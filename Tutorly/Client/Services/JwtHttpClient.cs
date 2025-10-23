using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.JSInterop;

namespace Tutorly.Client.Services
{
    public class JwtHttpClient
    {
        private readonly HttpClient _httpClient;
        private readonly IJSRuntime _jsRuntime;

        public JwtHttpClient(HttpClient httpClient, IJSRuntime jsRuntime)
        {
            _httpClient = httpClient;
            _jsRuntime = jsRuntime;
        }

        public async Task<HttpResponseMessage> GetAsync(string requestUri)
        {
            await AddJwtTokenAsync();
            return await _httpClient.GetAsync(requestUri);
        }

        public async Task<HttpResponseMessage> PostAsync(string requestUri, HttpContent content)
        {
            await AddJwtTokenAsync();
            return await _httpClient.PostAsync(requestUri, content);
        }

        public async Task<HttpResponseMessage> PutAsync(string requestUri, HttpContent content)
        {
            await AddJwtTokenAsync();
            return await _httpClient.PutAsync(requestUri, content);
        }

        public async Task<HttpResponseMessage> DeleteAsync(string requestUri)
        {
            await AddJwtTokenAsync();
            return await _httpClient.DeleteAsync(requestUri);
        }

        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            await AddJwtTokenAsync();
            return await _httpClient.SendAsync(request, cancellationToken);
        }

        // JSON extension methods
        public async Task<T?> GetFromJsonAsync<T>(string? requestUri, CancellationToken cancellationToken = default)
        {
            await AddJwtTokenAsync();
            return await _httpClient.GetFromJsonAsync<T>(requestUri, cancellationToken);
        }

        public async Task<HttpResponseMessage> PostAsJsonAsync<TValue>(string? requestUri, TValue value, CancellationToken cancellationToken = default)
        {
            await AddJwtTokenAsync();
            return await _httpClient.PostAsJsonAsync(requestUri, value, cancellationToken);
        }

        public async Task<HttpResponseMessage> PutAsJsonAsync<TValue>(string? requestUri, TValue value, CancellationToken cancellationToken = default)
        {
            await AddJwtTokenAsync();
            return await _httpClient.PutAsJsonAsync(requestUri, value, cancellationToken);
        }

        private async Task AddJwtTokenAsync()
        {
            try
            {
                // Clear any existing authorization header
                _httpClient.DefaultRequestHeaders.Authorization = null;

                var token = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "accessToken");
                Console.WriteLine($"JwtHttpClient: Token found: {!string.IsNullOrEmpty(token)}");

                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    Console.WriteLine($"JwtHttpClient: Authorization header set with Bearer token");
                }
                else
                {
                    Console.WriteLine($"JwtHttpClient: No access token found in localStorage");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"JwtHttpClient: Error getting token: {ex.Message}");
            }
        }
    }
}
