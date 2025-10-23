using System.Security.Claims;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Tutorly.Client.Services;
using Tutorly.Shared;

namespace Tutorly.Client
{
    public sealed class ServerAuthStateProvider : AuthenticationStateProvider
    {
        private readonly JwtHttpClient _http;
        private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

        public ServerAuthStateProvider(JwtHttpClient http) => _http = http;

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            try
            {
                Console.WriteLine("🔍 ServerAuthStateProvider: Getting authentication state...");
                var session = await _http.GetFromJsonAsync<UnifiedSessionDto>("api/auth/me");
                Console.WriteLine($"🔍 ServerAuthStateProvider: API response - User ID: {session?.User?.Id}, Email: {session?.User?.Email}, Role: {session?.Role}");

                if (session is null || string.IsNullOrWhiteSpace(session.User?.Id))
                {
                    Console.WriteLine("🔍 ServerAuthStateProvider: No user data, returning anonymous");
                    return new AuthenticationState(Anonymous);
                }

                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, session.User.Id)
                };

                if (!string.IsNullOrWhiteSpace(session.User.Email))
                    claims.Add(new(ClaimTypes.Email, session.User.Email));
                if (!string.IsNullOrWhiteSpace(session.Role))
                    claims.Add(new(ClaimTypes.Role, session.Role));

                var identity = new ClaimsIdentity(claims, authenticationType: "server");
                var principal = new ClaimsPrincipal(identity);
                Console.WriteLine($"🔍 ServerAuthStateProvider: Created authenticated principal with {claims.Count} claims");
                return new AuthenticationState(principal);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"🔍 ServerAuthStateProvider: Exception occurred: {ex.Message}");
                return new AuthenticationState(Anonymous);
            }
        }

        /// <summary>
        /// Async version you can await in components after sign-in/sign-out flows.
        /// </summary>
        public Task<AuthenticationState> RefreshAsync()
        {
            var task = GetAuthenticationStateAsync();
            NotifyAuthenticationStateChanged(task);
            return task;
        }

        /// <summary>
        /// Legacy sync helper; prefer RefreshAsync().
        /// </summary>
        public void Refresh() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
