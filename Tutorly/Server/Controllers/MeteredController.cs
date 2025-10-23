using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tutorly.Server.Services;
using Tutorly.Shared;

namespace Tutorly.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MeteredController : ControllerBase
{
    private readonly MeteredRoomService _meteredRoomService;
    private readonly ILogger<MeteredController> _logger;

    public MeteredController(
        MeteredRoomService meteredRoomService,
        ILogger<MeteredController> logger)
    {
        _meteredRoomService = meteredRoomService;
        _logger = logger;
    }

    [HttpPost("generate-access-token")]
    public async Task<ActionResult<MeteredAccessTokenResponse>> GenerateAccessToken([FromBody] MeteredAccessTokenRequest request)
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                        ?? User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("User ID not found in token");
            }

            var accessToken = await _meteredRoomService.GenerateAccessTokenAsync(
                request.RoomUrl,
                request.UserName,
                userId,
                request.DurationMinutes);

            if (string.IsNullOrEmpty(accessToken))
            {
                return BadRequest("Failed to generate access token");
            }

            return Ok(new MeteredAccessTokenResponse
            {
                AccessToken = accessToken,
                RoomUrl = request.RoomUrl,
                ExpiresIn = request.DurationMinutes * 60 // Convert to seconds
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Metered access token");
            return StatusCode(500, "Error generating access token");
        }
    }

    [HttpPost("create-room")]
    public async Task<ActionResult<MeteredRoomResponse>> CreateRoom([FromBody] MeteredCreateRoomRequest request)
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                        ?? User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("User ID not found in token");
            }

            var roomId = Guid.NewGuid();
            var roomUrl = await _meteredRoomService.CreateRoomAsync(roomId, request.RoomName, request.Privacy);

            if (string.IsNullOrEmpty(roomUrl))
            {
                return BadRequest("Failed to create Metered room");
            }

            return Ok(new MeteredRoomResponse
            {
                RoomId = roomId,
                RoomUrl = roomUrl,
                RoomName = request.RoomName,
                Privacy = request.Privacy
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Metered room");
            return StatusCode(500, "Error creating room");
        }
    }
}

public class MeteredAccessTokenRequest
{
    public string RoomUrl { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public int DurationMinutes { get; set; } = 1440; // 24 hours default
}

public class MeteredAccessTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RoomUrl { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
}

public class MeteredCreateRoomRequest
{
    public string RoomName { get; set; } = string.Empty;
    public string Privacy { get; set; } = "private";
}

public class MeteredRoomResponse
{
    public Guid RoomId { get; set; }
    public string RoomUrl { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public string Privacy { get; set; } = string.Empty;
}
