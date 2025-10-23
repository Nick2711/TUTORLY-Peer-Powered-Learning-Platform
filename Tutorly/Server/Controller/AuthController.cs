using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Tutorly.Server.Helpers;
using Tutorly.Server.Services;
using Tutorly.Shared;

namespace Tutorly.Server.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly SupabaseSettings _sb;
        private readonly IConfiguration _cfg;
        private readonly IUnifiedAuthService _unifiedAuthService;

        public AuthController(IOptions<SupabaseSettings> sb, IConfiguration cfg, IUnifiedAuthService unifiedAuthService)
        {
            _sb = sb.Value;
            _cfg = cfg;
            _unifiedAuthService = unifiedAuthService;
        }

        public record SignUpDto(string Email, string Password, string? Role);
        public record SignInDto(string Email, string Password);


        [HttpPost("signup")]
        public async Task<IActionResult> SignUp([FromBody] SignUpDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest("Email and password are required.");

            var baseUrl = _sb.Url.TrimEnd('/');

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _sb.AnonKey);
            http.DefaultRequestHeaders.Add("apikey", _sb.AnonKey);

            // Supabase expects email/password at top-level; custom fields go into "data" (user_metadata)
            var payload = new
            {
                email = dto.Email,
                password = dto.Password,
                data = new Dictionary<string, object?> { ["role"] = dto.Role ?? "student" },
                email_redirect_to = $"{Request.Scheme}://{Request.Host}/auth/confirmed"
            };

            var resp = await http.PostAsJsonAsync($"{baseUrl}/auth/v1/signup", payload);
            var content = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                return StatusCode((int)resp.StatusCode, content);

            // IMPORTANT for OTP-first flow: don't set auth cookies here even if session returned.
            // Client will do OTP and then sign in.
            using var doc = JsonDocument.Parse(content);
            bool emailConfirm = !(doc.RootElement.TryGetProperty("session", out var _));

            return Ok(new { created = true, requiresEmailConfirm = emailConfirm });
        }



        [HttpPost("signin")]
        public async Task<IActionResult> SignIn([FromBody] SignInDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest("Email and password are required.");

            var baseUrl = _sb.Url.TrimEnd('/');

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _sb.AnonKey);
            http.DefaultRequestHeaders.Add("apikey", _sb.AnonKey);

            var payload = new { email = dto.Email, password = dto.Password };
            var resp = await http.PostAsJsonAsync($"{baseUrl}/auth/v1/token?grant_type=password", payload);
            var content = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                return StatusCode((int)resp.StatusCode, content);

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            // set cookies from token response
            SetAuthCookiesFromSession(root);

            // Get user info and resolve role/profile
            var access = root.GetProperty("access_token").GetString();
            var (id, email) = await GetUserFromAccessToken(access!);
            Console.WriteLine($"SignIn: User ID: {id}, Email: {email}");

            if (!string.IsNullOrWhiteSpace(id))
            {
                // Use unified auth service to resolve role and profile
                var session = await _unifiedAuthService.GetUserSessionAsync(id, email);
                if (session == null)
                {
                    Console.WriteLine($"SignIn: No profile found for user {id}");
                    return Unauthorized("No profile found for this account. Please contact support.");
                }

                Console.WriteLine($"SignIn: User authenticated as {session.Role}");

                // Return the session with the JWT token
                var response = new
                {
                    session.Role,
                    session.Profile.FullName,
                    session.Profile.AvatarUrl,
                    session.Profile.CreatedAt,
                    session.Profile.StudentId,
                    session.Profile.TutorId,
                    session.Profile.Programme,
                    AccessToken = access,
                    RefreshToken = root.GetProperty("refresh_token").GetString()
                };

                return Ok(response);
            }

            return Unauthorized("Authentication failed.");
        }

        // Called by Redirect page
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var access = Request.Cookies["sb-access-token"];
            if (string.IsNullOrWhiteSpace(access))
                return Unauthorized();

            var (id, email) = await GetUserFromAccessToken(access);
            if (string.IsNullOrWhiteSpace(id))
                return Unauthorized();

            // Use unified auth service to get complete session
            var session = await _unifiedAuthService.GetUserSessionAsync(id, email);
            if (session == null)
                return Unauthorized("No profile found for this account.");

            return Ok(session);
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh()
        {
            var baseUrl = _sb.Url.TrimEnd('/');
            var refresh = Request.Cookies["sb-refresh-token"];
            if (string.IsNullOrWhiteSpace(refresh))
                return Unauthorized();

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _sb.AnonKey);
            http.DefaultRequestHeaders.Add("apikey", _sb.AnonKey);

            var payload = new { refresh_token = refresh };
            var resp = await http.PostAsJsonAsync($"{baseUrl}/auth/v1/token?grant_type=refresh_token", payload);
            var content = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                return Unauthorized();

            using var doc = JsonDocument.Parse(content);
            SetAuthCookiesFromSession(doc.RootElement);
            return Ok(new { ok = true });
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            var cookieDomain = _cfg["Auth:CookieDomain"];
            var secure = bool.TryParse(_cfg["Auth:CookieSecure"], out var s) && s;
            var sameSite = _cfg["Auth:CookieSameSite"]?.ToLower() == "none" ? SameSiteMode.None : SameSiteMode.Lax;

            Response.Cookies.Delete("sb-access-token", new CookieOptions
            {
                HttpOnly = true,
                Secure = secure,
                SameSite = sameSite,
                Domain = cookieDomain,
                Path = "/"
            });
            Response.Cookies.Delete("sb-refresh-token", new CookieOptions
            {
                HttpOnly = true,
                Secure = secure,
                SameSite = sameSite,
                Domain = cookieDomain,
                Path = "/"
            });

            return Ok(new { ok = true });
        }

        // ---------- Helpers ----------

        private async Task<(string id, string? email)> GetUserFromAccessToken(string accessToken)
        {
            var baseUrl = _sb.Url.TrimEnd('/');

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            http.DefaultRequestHeaders.Add("apikey", _sb.AnonKey);

            var resp = await http.GetAsync($"{baseUrl}/auth/v1/user");
            if (!resp.IsSuccessStatusCode)
                return ("", null);

            var txt = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(txt);
            var root = doc.RootElement;

            var id = root.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
            var email = root.TryGetProperty("email", out var emEl) ? emEl.GetString() : null;

            return (id, email);
        }


        private void SetAuthCookiesFromSession(JsonElement sessionOrTokenPayload)
        {
            var access = sessionOrTokenPayload.TryGetProperty("access_token", out var at) ? at.GetString() : null;
            var refresh = sessionOrTokenPayload.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;

            long expiresIn = sessionOrTokenPayload.TryGetProperty("expires_in", out var ei) && ei.TryGetInt64(out var eVal) ? eVal : 3600;

            var cookieDomain = _cfg["Auth:CookieDomain"];
            var secure = bool.TryParse(_cfg["Auth:CookieSecure"], out var s) && s;
            var sameSite = _cfg["Auth:CookieSameSite"]?.ToLower() == "none" ? SameSiteMode.None : SameSiteMode.Lax;

            if (!string.IsNullOrWhiteSpace(access))
            {
                Response.Cookies.Append("sb-access-token", access!, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = secure,
                    SameSite = sameSite,
                    Domain = cookieDomain,
                    Path = "/",
                    Expires = DateTimeOffset.UtcNow.AddSeconds(expiresIn)
                });
            }

            if (!string.IsNullOrWhiteSpace(refresh))
            {
                Response.Cookies.Append("sb-refresh-token", refresh!, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = secure,
                    SameSite = sameSite,
                    Domain = cookieDomain,
                    Path = "/",
                    Expires = DateTimeOffset.UtcNow.AddDays(30)
                });
            }
        }
    }
}
