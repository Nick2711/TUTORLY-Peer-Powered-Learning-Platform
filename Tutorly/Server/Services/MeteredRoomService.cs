using System.Text.Json;

namespace Tutorly.Server.Services;

public class MeteredRoomService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MeteredRoomService> _logger;

    public MeteredRoomService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<MeteredRoomService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string?> CreateRoomAsync(Guid roomId, string roomName, string privacy = "private")
    {
        try
        {
            var meteredApiKey = _configuration["Metered:ApiKey"];
            var meteredDomain = _configuration["Metered:Domain"] ?? "tutorly-rtc.metered.live";

            if (string.IsNullOrEmpty(meteredApiKey))
            {
                _logger.LogWarning("Metered API key not configured, skipping Metered room creation");
                return null;
            }

            // For Metered, we can generate room URLs directly without API calls
            // The room will be created when the first participant joins
            var roomUrl = $"https://{meteredDomain}/{roomId}";

            _logger.LogInformation($"Generated Metered room URL: {roomUrl}");
            return roomUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Metered room");
        }

        return null;
    }

    public async Task<bool> DeleteRoomAsync(string roomUrl)
    {
        try
        {
            var meteredApiKey = _configuration["Metered:ApiKey"];

            if (string.IsNullOrEmpty(meteredApiKey))
            {
                _logger.LogWarning("Metered API key not configured");
                return false;
            }

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", meteredApiKey);

            var meteredDomain = _configuration["Metered:Domain"] ?? "tutorly-rtc.metered.live";
            var response = await _httpClient.DeleteAsync($"https://{meteredDomain}/api/v1/rooms/{roomUrl}");

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Metered room deleted: {roomUrl}");
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Failed to delete Metered room: {response.StatusCode} - {errorContent}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting Metered room");
        }

        return false;
    }

    public async Task<string?> GenerateAccessTokenAsync(string roomUrl, string userName, string userId, int durationMinutes = 1440)
    {
        try
        {
            var meteredApiKey = _configuration["Metered:ApiKey"];

            if (string.IsNullOrEmpty(meteredApiKey))
            {
                _logger.LogWarning("Metered API key not configured");
                return null;
            }

            // For Metered, we can generate access tokens using their API
            var tokenData = new
            {
                roomURL = roomUrl,
                name = userName,
                userId = userId,
                duration = durationMinutes,
                canPublish = true,
                canSubscribe = true
            };

            var json = JsonSerializer.Serialize(tokenData);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", meteredApiKey);

            var meteredDomain = _configuration["Metered:Domain"] ?? "tutorly-rtc.metered.live";
            var response = await _httpClient.PostAsync($"https://{meteredDomain}/api/v1/token?secretKey={meteredApiKey}", content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

                if (tokenResponse.TryGetProperty("token", out var tokenElement))
                {
                    var token = tokenElement.GetString();
                    _logger.LogInformation($"Metered access token generated for user {userId}");
                    return token;
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Failed to generate Metered access token: {response.StatusCode} - {errorContent}");

                // Fallback: return null to use room without token
                _logger.LogInformation("Using room without access token");
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Metered access token");
        }

        return null;
    }
}
