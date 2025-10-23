using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Tutorly.Server.Helpers;

public class CustomUserIdProvider : IUserIdProvider
{
    private readonly ILogger<CustomUserIdProvider> _logger;

    public CustomUserIdProvider(ILogger<CustomUserIdProvider> logger)
    {
        _logger = logger;
    }

    public string? GetUserId(HubConnectionContext connection)
    {
        var userId = connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? connection.User?.FindFirst("sub")?.Value;

        _logger.LogInformation($"CustomUserIdProvider: Connection {connection.ConnectionId} -> UserId: {userId}");

        if (connection.User?.Claims != null)
        {
            _logger.LogInformation($"CustomUserIdProvider: Available claims: {string.Join(", ", connection.User.Claims.Select(c => $"{c.Type}={c.Value}"))}");
        }

        return userId;
    }
}
