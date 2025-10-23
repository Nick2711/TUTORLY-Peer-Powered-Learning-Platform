using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Tutorly.Server.Helpers;
using Tutorly.Shared;

namespace Tutorly.Server.Services
{
    public interface ISupabaseAuthService
    {
        Task<string?> GetUserEmailByUserIdAsync(string userId);
        Task<ServiceResult<string>> CreateUserAsync(string email, string password);
        Task<ServiceResult> DeleteUserAsync(string userId); // Added delete method
    }

    public class SupabaseAuthService : ISupabaseAuthService
    {
        private readonly SupabaseSettings _settings;
        private readonly ILogger<SupabaseAuthService> _logger;
        private readonly HttpClient _httpClient;

        public SupabaseAuthService(
            IOptions<SupabaseSettings> settings,
            ILogger<SupabaseAuthService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _settings = settings.Value;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
        }

        public async Task<string?> GetUserEmailByUserIdAsync(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(_settings.ServiceRoleKey))
                {
                    _logger.LogWarning("ServiceRoleKey is not configured. Cannot fetch user email.");
                    return null;
                }

                var baseUrl = _settings.Url.TrimEnd('/');
                var url = $"{baseUrl}/auth/v1/admin/users/{Uri.EscapeDataString(userId)}";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ServiceRoleKey);
                request.Headers.Add("apikey", _settings.ServiceRoleKey);

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"Failed to fetch user email for userId {userId}: {errorContent}");
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);

                if (doc.RootElement.TryGetProperty("email", out var emailElement) &&
                    emailElement.ValueKind == JsonValueKind.String)
                {
                    return emailElement.GetString();
                }

                _logger.LogWarning($"No email found for userId {userId}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching email for userId {userId}");
                return null;
            }
        }

        public async Task<ServiceResult<string>> CreateUserAsync(string email, string password)
        {
            try
            {
                var url = $"{_settings.Url}/auth/v1/admin/users";
                var payload = new
                {
                    email = email,
                    password = password,
                    email_confirm = true
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ServiceRoleKey);
                _httpClient.DefaultRequestHeaders.Add("apikey", _settings.ServiceRoleKey);

                var response = await _httpClient.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    // Supabase Auth returns the user object directly, not nested under "user"
                    if (result.TryGetProperty("id", out var idElement))
                    {
                        var userId = idElement.GetString();
                        _logger.LogInformation("Successfully created user with email {Email}", email);
                        return ServiceResult<string>.SuccessResult(userId ?? string.Empty);
                    }
                }

                _logger.LogError("Failed to create user. Response: {Response}", responseContent);
                return ServiceResult<string>.FailureResult("Failed to create user in authentication system");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user with email {Email}", email);
                return ServiceResult<string>.FailureResult("Failed to create user");
            }
        }

        public async Task<ServiceResult> DeleteUserAsync(string userId)
        {
            try
            {
                var url = $"{_settings.Url}/auth/v1/admin/users/{Uri.EscapeDataString(userId)}";

                using var request = new HttpRequestMessage(HttpMethod.Delete, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ServiceRoleKey);
                request.Headers.Add("apikey", _settings.ServiceRoleKey);

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully deleted user {UserId}", userId);
                    return ServiceResult.SuccessResult();
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to delete user {UserId}. Response: {Response}", userId, errorContent);
                return ServiceResult.FailureResult($"Failed to delete user: {errorContent}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}", userId);
                return ServiceResult.FailureResult("Failed to delete user");
            }
        }
    }
}

