using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Net.Http.Json; // for PostAsJsonAsync / PatchAsJsonAsync
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Tutorly.Server.Helpers;

namespace Tutorly.Server.Controller
{
    [ApiController]
    [Route("api/otp")]
    public class OtpController : ControllerBase
    {
        private readonly SupabaseSettings _sb;
        private readonly IConfiguration _cfg;
        private readonly OtpSettings _otp;
        private readonly SmtpSettings _smtp;
        private readonly bool _isDev;

        public OtpController(
            IOptions<SupabaseSettings> sb,
            IConfiguration cfg)
        {
            _sb = sb.Value;
            _cfg = cfg;
            _otp = _cfg.GetSection("Otp").Get<OtpSettings>() ?? new();
            _smtp = _cfg.GetSection("Smtp").Get<SmtpSettings>() ?? new();
            _isDev = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        }

        public record SendDto(string Email);
        //include Password (+ optional FullName) so we can create the user only after OTP passes
        public record VerifyDto(string Email, string Code, string Password, string? FullName, string? Role);

        [HttpPost("send")]
        public async Task<IActionResult> Send([FromBody] SendDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || !dto.Email.Contains('@'))
                return BadRequest("Invalid email.");

            var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString("D6");
            var hash = HashCode(dto.Email, code, _otp.Secret);
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_otp.ExpiryMinutes);

            var baseUrl = _sb.Url.TrimEnd('/');
            var adminKey = string.IsNullOrWhiteSpace(_sb.ServiceRoleKey) ? _sb.AnonKey : _sb.ServiceRoleKey;

            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminKey);
                http.DefaultRequestHeaders.Add("apikey", adminKey);
                http.DefaultRequestHeaders.Add("Prefer", "return=representation");

                var payload = new[]
                {
                    new {
                        email = dto.Email,
                        code_hash = hash,
                        expires_at = expiresAt.UtcDateTime
                    }
                };

                var resp = await http.PostAsJsonAsync($"{baseUrl}/rest/v1/email_otp", payload);
                if (!resp.IsSuccessStatusCode)
                {
                    var txt = await resp.Content.ReadAsStringAsync();
                    return StatusCode((int)resp.StatusCode, txt);
                }
            }

            // email the code
            var subject = "Your Tutorly verification code";
            var html = $@"
<div style=""font-family:Inter,system-ui,-apple-system,Segoe UI,Roboto,Arial,sans-serif;max-width:560px;margin:0 auto;padding:24px"">
  <h2 style=""margin:0 0 12px"">Verify your email</h2>
  <p style=""margin:0 0 16px;color:#555"">Enter this 6-digit code in the app:</p>
  <div style=""font-size:28px;letter-spacing:8px;font-weight:700;margin:16px 0"">{code}</div>
  <p style=""margin:0;color:#777"">This code expires in {_otp.ExpiryMinutes} minutes.</p>
</div>";

            try
            {
                await SendEmailAsync(dto.Email, subject, html);
            }
            catch
            {
                if (!_isDev) throw;
            }

            if (_isDev)
                Console.WriteLine($"[DEV] OTP for {dto.Email}: {code}");

            return Ok(new { sent = true, seconds = _otp.ExpiryMinutes * 60 });
        }

        [HttpPost("verify")]
        public async Task<IActionResult> Verify([FromBody] VerifyDto dto)
        {
            Console.WriteLine($"[OTP Verify] Starting verification for: {dto.Email}");

            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Code) || string.IsNullOrWhiteSpace(dto.Password))
            {
                Console.WriteLine("[OTP Verify] ERROR: Missing email, code or password");
                return BadRequest("Missing email, code or password.");
            }

            var baseUrl = _sb.Url.TrimEnd('/');
            var adminKey = string.IsNullOrWhiteSpace(_sb.ServiceRoleKey) ? _sb.AnonKey : _sb.ServiceRoleKey;

            try
            {
                Console.WriteLine("[OTP Verify] Starting OTP verification process...");
                int attempts = 0;
                string id = ""; // UUID-safe
                string codeHash = "";
                DateTime expiresAtUtc;

                using (var http = new HttpClient())
                {
                    http.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminKey);
                    http.DefaultRequestHeaders.Add("apikey", adminKey);

                    // fetch latest, unconsumed OTP row
                    var url =
                        $"{baseUrl}/rest/v1/email_otp" +
                        $"?email=eq.{Uri.EscapeDataString(dto.Email)}" +
                        $"&consumed=eq.false" +
                        $"&select=id,code_hash,expires_at,attempts" +
                        $"&order=created_at.desc" +
                        $"&limit=1";

                    var resp = await http.GetAsync(url);
                    if (!resp.IsSuccessStatusCode) return Unauthorized(new { ok = false });

                    var txt = await resp.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(txt);
                    if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                        return Unauthorized(new { ok = false, reason = "not_found" });

                    var row = doc.RootElement[0];

                    var idEl = row.GetProperty("id");
                    if (idEl.ValueKind == JsonValueKind.String) id = idEl.GetString()!;
                    else if (idEl.ValueKind == JsonValueKind.Number) id = idEl.GetInt64().ToString();
                    else return StatusCode(500, "Unexpected id type");

                    codeHash = row.TryGetProperty("code_hash", out var ch) && ch.ValueKind == JsonValueKind.String ? ch.GetString()! : "";
                    attempts = row.TryGetProperty("attempts", out var att) && att.ValueKind == JsonValueKind.Number ? att.GetInt32() : 0;

                    if (!(row.TryGetProperty("expires_at", out var expEl) && expEl.ValueKind == JsonValueKind.String &&
                          DateTime.TryParse(expEl.GetString(), out var expParsed)))
                        return StatusCode(500, "Invalid expires_at");

                    expiresAtUtc = DateTime.SpecifyKind(expParsed, DateTimeKind.Utc);

                    // expiry / lockout
                    if (DateTime.UtcNow > expiresAtUtc) return Unauthorized(new { ok = false, reason = "expired" });
                    if (attempts >= _otp.MaxAttempts) return Unauthorized(new { ok = false, reason = "locked" });

                    var providedHash = HashCode(dto.Email, dto.Code, _otp.Secret);
                    var match = SlowEquals(codeHash, providedHash);

                    http.DefaultRequestHeaders.Remove("Prefer");
                    http.DefaultRequestHeaders.Add("Prefer", "return=representation");

                    if (match)
                    {
                        Console.WriteLine("[OTP Verify] Code matched! Consuming OTP...");

                        // consume otp
                        var consume = new[] { new { consumed = true, attempts } };
                        var put = await http.PatchAsJsonAsync(
                            $"{baseUrl}/rest/v1/email_otp?id=eq.{Uri.EscapeDataString(id)}",
                            consume
                        );
                        if (!put.IsSuccessStatusCode)
                        {
                            Console.WriteLine($"[OTP Verify] ERROR: Failed to consume OTP: {await put.Content.ReadAsStringAsync()}");
                            return StatusCode((int)put.StatusCode, await put.Content.ReadAsStringAsync());
                        }

                        Console.WriteLine("[OTP Verify] Creating Supabase user...");

                        // Create user only now (2FA gate)
                        var ensured = await EnsureSupabaseUserExistsOrCreateAsync(dto.Email, dto.Password, dto.FullName, dto.Role);
                        if (!ensured)
                        {
                            Console.WriteLine("[OTP Verify] ERROR: Failed to create Supabase user");
                            return StatusCode(500, "Could not create/confirm auth user. Check service_role key.");
                        }

                        Console.WriteLine("[OTP Verify] User created successfully!");

                        // (Optional) If you maintain a profiles row, insert it here using service_role
                        // await EnsureProfileRowAsync(userId, dto.Email, dto.FullName);

                        return Ok(new { ok = true });
                    }
                    else
                    {
                        // bump attempts
                        var bump = new[] { new { attempts = attempts + 1 } };
                        var put = await http.PatchAsJsonAsync(
                            $"{baseUrl}/rest/v1/email_otp?id=eq.{Uri.EscapeDataString(id)}",
                            bump
                        );
                        if (!put.IsSuccessStatusCode)
                            return StatusCode((int)put.StatusCode, await put.Content.ReadAsStringAsync());

                        return Unauthorized(new { ok = false, reason = "mismatch" });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OTP Verify] EXCEPTION: {ex.Message}");
                Console.WriteLine($"[OTP Verify] Stack trace: {ex.StackTrace}");
                Console.WriteLine($"[OTP Verify] Inner exception: {ex.InnerException?.Message}");

                var msg = _isDev ? $"{ex.Message}\n{ex.StackTrace}" : "OTP verification failed.";
                return StatusCode(500, msg);
            }
        }

        // ---------- helpers ----------
        private static string HashCode(string email, string code, string secret)
        {
            using var sha = SHA256.Create();
            var input = Encoding.UTF8.GetBytes($"{email}|{code}|{secret}");
            var hash = sha.ComputeHash(input);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static bool SlowEquals(string a, string b)
        {
            var diff = (uint)a.Length ^ (uint)b.Length;
            for (int i = 0; i < a.Length && i < b.Length; i++)
                diff |= (uint)(a[i] ^ b[i]);
            return diff == 0;
        }

        private async Task SendEmailAsync(string to, string subject, string html)
        {
            if (string.IsNullOrWhiteSpace(_smtp.Host))
                throw new InvalidOperationException("SMTP not configured.");

            using var client = new SmtpClient(_smtp.Host, _smtp.Port)
            {
                Credentials = new NetworkCredential(_smtp.User, _smtp.Pass),
                EnableSsl = _smtp.EnableSsl
            };
            var msg = new MailMessage(_smtp.From ?? _smtp.User, to, subject, html)
            {
                IsBodyHtml = true
            };
            await client.SendMailAsync(msg);
        }

        // Ensure user exists; if not, create with password and confirm. If exists but unconfirmed, confirm.
        private async Task<bool> EnsureSupabaseUserExistsOrCreateAsync(string email, string password, string? fullName, string? role)
        {
            try
            {
                Console.WriteLine($"[EnsureUser] Starting for email: {email}");

                var baseUrl = _sb.Url.TrimEnd('/');
                var adminKey = _sb.ServiceRoleKey;

                if (string.IsNullOrWhiteSpace(adminKey))
                {
                    Console.WriteLine("[EnsureUser] ERROR: ServiceRoleKey is missing!");
                    return false;
                }

                Console.WriteLine("[EnsureUser] ServiceRoleKey found, creating HTTP client...");

                using var http = new HttpClient();
                http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminKey);
                http.DefaultRequestHeaders.Add("apikey", adminKey);

                // 1) lookup - NOTE: Admin API doesn't use PostgREST syntax, just direct params
                Console.WriteLine($"[EnsureUser] Looking up existing user: {email}");

                // Get all users and filter in code (Admin API limitation)
                var find = await http.GetAsync($"{baseUrl}/auth/v1/admin/users");

                if (!find.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[EnsureUser] ERROR: Lookup failed with status {find.StatusCode}");
                    Console.WriteLine($"[EnsureUser] Response: {await find.Content.ReadAsStringAsync()}");
                    return false;
                }

                var txt = await find.Content.ReadAsStringAsync();
                Console.WriteLine($"[EnsureUser] Lookup response length: {txt.Length} chars");

                using var doc = JsonDocument.Parse(txt);

                // The response is {"users": [...], "aud": "..."}
                JsonElement? matchedUser = null;

                if (doc.RootElement.TryGetProperty("users", out var usersArray) &&
                    usersArray.ValueKind == JsonValueKind.Array)
                {
                    Console.WriteLine($"[EnsureUser] Found {usersArray.GetArrayLength()} total users, filtering for {email}...");

                    // Find the user with matching email
                    foreach (var user in usersArray.EnumerateArray())
                    {
                        if (user.TryGetProperty("email", out var emailProp) &&
                            emailProp.GetString()?.Equals(email, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            matchedUser = user;
                            Console.WriteLine($"[EnsureUser] Found matching user!");
                            break;
                        }
                    }
                }

                if (matchedUser.HasValue)
                {
                    Console.WriteLine("[EnsureUser] User already exists, checking confirmation...");

                    var user = matchedUser.Value;
                    var userId = user.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String
                        ? idEl.GetString()!
                        : null;

                    var confirmedAtExists = user.TryGetProperty("confirmed_at", out var confEl) && confEl.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(confEl.GetString());

                    if (!confirmedAtExists && userId != null)
                    {
                        Console.WriteLine($"[EnsureUser] User exists but not confirmed, confirming user {userId}...");
                        var confirmPayload = new { email_confirm = true };
                        var patch = await http.PatchAsJsonAsync($"{baseUrl}/auth/v1/admin/users/{Uri.EscapeDataString(userId)}", confirmPayload);

                        if (patch.IsSuccessStatusCode)
                        {
                            Console.WriteLine("[EnsureUser] User confirmed successfully!");
                            return true;
                        }
                        else
                        {
                            Console.WriteLine($"[EnsureUser] ERROR: Failed to confirm user: {await patch.Content.ReadAsStringAsync()}");
                            return false;
                        }
                    }

                    Console.WriteLine("[EnsureUser] User already exists and is confirmed!");
                    return true; // exists and (likely) confirmed
                }

                // 2) create (admin) with email_confirm = true
                Console.WriteLine("[EnsureUser] User doesn't exist, creating new user...");
                Console.WriteLine($"[EnsureUser] Email: {email}");
                Console.WriteLine($"[EnsureUser] Password length: {password?.Length ?? 0}");
                Console.WriteLine($"[EnsureUser] Full name: {fullName}");
                Console.WriteLine($"[EnsureUser] Role: {role ?? "student"}");

                var createPayload = new
                {
                    email = email,
                    password = password,
                    email_confirm = true,
                    user_metadata = new
                    {
                        full_name = fullName ?? "",
                        role = role ?? "student"  // Include role for trigger
                    }
                };

                Console.WriteLine($"[EnsureUser] Payload: {System.Text.Json.JsonSerializer.Serialize(createPayload)}");
                Console.WriteLine($"[EnsureUser] Posting to: {baseUrl}/auth/v1/admin/users");

                var create = await http.PostAsJsonAsync($"{baseUrl}/auth/v1/admin/users", createPayload);

                if (create.IsSuccessStatusCode)
                {
                    Console.WriteLine("[EnsureUser] User created successfully!");
                    var response = await create.Content.ReadAsStringAsync();
                    Console.WriteLine($"[EnsureUser] Create response: {response}");
                    return true;
                }
                else
                {
                    Console.WriteLine($"[EnsureUser] ERROR: Failed to create user - Status: {create.StatusCode}");
                    var errorResponse = await create.Content.ReadAsStringAsync();
                    Console.WriteLine($"[EnsureUser] Response: {errorResponse}");
                    Console.WriteLine($"[EnsureUser] Response Headers: {string.Join(", ", create.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}"))}");

                    // Try to parse error details
                    try
                    {
                        using var errorDoc = JsonDocument.Parse(errorResponse);
                        if (errorDoc.RootElement.TryGetProperty("msg", out var msg))
                            Console.WriteLine($"[EnsureUser] Error message: {msg.GetString()}");
                        if (errorDoc.RootElement.TryGetProperty("error_code", out var code))
                            Console.WriteLine($"[EnsureUser] Error code: {code.GetString()}");
                    }
                    catch { }

                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EnsureUser] EXCEPTION: {ex.Message}");
                Console.WriteLine($"[EnsureUser] Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        private sealed class OtpSettings
        {
            public string Secret { get; set; } = "dev-only-change-me";
            public int ExpiryMinutes { get; set; } = 10;
            public int MaxAttempts { get; set; } = 5;
        }
        private sealed class SmtpSettings
        {
            public string Host { get; set; } = "";
            public int Port { get; set; } = 587;
            public string User { get; set; } = "";
            public string Pass { get; set; } = "";
            public string? From { get; set; }
            public bool EnableSsl { get; set; } = true;
        }
    }
}
