using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Tutorly.Server.Services;

namespace Tutorly.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class WebRTCController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<WebRTCController> _logger;
    private readonly IStudyRoomService _studyRoomService;

    public WebRTCController(IConfiguration configuration, ILogger<WebRTCController> logger, IStudyRoomService studyRoomService)
    {
        _configuration = configuration;
        _logger = logger;
        _studyRoomService = studyRoomService;
    }

    /// <summary>
    /// Get TURN server credentials for WebRTC
    /// </summary>
    [HttpGet("turn-credentials")]
    public ActionResult<TurnCredentialsResponse> GetTurnCredentials()
    {
        try
        {
            // Get TURN credentials from configuration
            var turnUsername = _configuration["TurnServer:Username"];
            var turnPassword = _configuration["TurnServer:Password"];
            var turnServer = _configuration["TurnServer:Server"] ?? "standard.relay.metered.ca";

            if (string.IsNullOrEmpty(turnUsername) || string.IsNullOrEmpty(turnPassword))
            {
                return BadRequest("TURN server credentials not configured");
            }

            return Ok(new TurnCredentialsResponse
            {
                IceServers = new[]
                {
                    new IceServer { Urls = "stun:stun.relay.metered.ca:80" },
                    new IceServer { Urls = $"turn:{turnServer}:80", Username = turnUsername, Credential = turnPassword },
                    new IceServer { Urls = $"turn:{turnServer}:80?transport=tcp", Username = turnUsername, Credential = turnPassword },
                    new IceServer { Urls = $"turn:{turnServer}:443", Username = turnUsername, Credential = turnPassword },
                    new IceServer { Urls = $"turns:{turnServer}:443?transport=tcp", Username = turnUsername, Credential = turnPassword }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting TURN credentials");
            return StatusCode(500, "Error retrieving TURN credentials");
        }
    }

    /// <summary>
    /// Clean up all empty rooms (admin endpoint)
    /// </summary>
    [HttpPost("cleanup-empty-rooms")]
    public async Task<ActionResult> CleanupEmptyRooms()
    {
        try
        {
            // This would typically require admin authorization
            // For now, we'll make it available to authenticated users
            // In production, add [Authorize(Roles = "Admin")] attribute

            await _studyRoomService.CleanupAllEmptyRoomsAsync();
            return Ok("Cleanup completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up empty rooms");
            return StatusCode(500, "Error cleaning up empty rooms");
        }
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? User.FindFirst("sub")?.Value;

        if (Guid.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }

        return Guid.Empty;
    }
}

public class TurnCredentialsResponse
{
    public IceServer[] IceServers { get; set; } = Array.Empty<IceServer>();
}

public class IceServer
{
    public string Urls { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Credential { get; set; }
}
