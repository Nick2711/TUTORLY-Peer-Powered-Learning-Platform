using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Tutorly.Server.Services;
using Tutorly.Shared;

namespace Tutorly.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class StudyRoomController : ControllerBase
{
    private readonly IStudyRoomService _roomService;
    private readonly ILogger<StudyRoomController> _logger;

    public StudyRoomController(IStudyRoomService roomService, ILogger<StudyRoomController> logger)
    {
        _roomService = roomService;
        _logger = logger;
    }

    /// <summary>
    /// Get all available study rooms with optional filters
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<StudyRoomDto>>> GetAvailableRooms(
        [FromQuery] string? roomType = null,
        [FromQuery] string? privacy = null,
        [FromQuery] Guid? moduleId = null,
        [FromQuery] string? status = null,
        [FromQuery] bool? onlyMyRooms = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User not authenticated");
            }

            var filters = new StudyRoomFilters
            {
                RoomType = roomType,
                Privacy = privacy,
                ModuleId = moduleId,
                Status = status,
                OnlyMyRooms = onlyMyRooms
            };

            var rooms = await _roomService.GetAvailableRoomsAsync(filters, userId);
            return Ok(rooms);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available rooms");
            return StatusCode(500, "Error retrieving rooms");
        }
    }

    /// <summary>
    /// Get a specific study room by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<StudyRoomDto>> GetRoomById(Guid id)
    {
        try
        {
            var room = await _roomService.GetRoomByIdAsync(id);
            if (room == null)
            {
                return NotFound("Room not found");
            }

            return Ok(room);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting room {id}");
            return StatusCode(500, "Error retrieving room");
        }
    }

    /// <summary>
    /// Get user's scheduled sessions and rooms they're participating in
    /// </summary>
    [HttpGet("my-sessions")]
    public async Task<ActionResult<List<StudyRoomDto>>> GetMySessions()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User not authenticated");
            }

            var sessions = await _roomService.GetMySessionsAsync(userId);
            return Ok(sessions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user sessions");
            return StatusCode(500, "Error retrieving sessions");
        }
    }

    /// <summary>
    /// Create a new study room
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CreateStudyRoomResponse>> CreateRoom([FromBody] CreateStudyRoomRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User not authenticated");
            }

            if (string.IsNullOrWhiteSpace(request.RoomName))
            {
                return BadRequest("Room name is required");
            }

            var response = await _roomService.CreateRoomAsync(request, userId);

            if (!response.Success)
            {
                return BadRequest(response.Message);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating room");
            return StatusCode(500, "Error creating room");
        }
    }

    /// <summary>
    /// Delete/End a study room (creator only)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteRoom(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User not authenticated");
            }

            var result = await _roomService.DeleteRoomAsync(id, userId);

            if (!result)
            {
                return BadRequest("Unable to delete room. You may not have permission.");
            }

            return Ok(new { success = true, message = "Room ended successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting room {id}");
            return StatusCode(500, "Error deleting room");
        }
    }

    /// <summary>
    /// Update room status
    /// </summary>
    [HttpPatch("{id}/status")]
    public async Task<ActionResult> UpdateRoomStatus(Guid id, [FromBody] UpdateRoomStatusRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User not authenticated");
            }

            var result = await _roomService.UpdateRoomStatusAsync(id, request.Status);

            if (!result)
            {
                return BadRequest("Unable to update room status");
            }

            return Ok(new { success = true, message = "Room status updated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating room status {id}");
            return StatusCode(500, "Error updating room status");
        }
    }

    /// <summary>
    /// Get participants in a room
    /// </summary>
    [HttpGet("{id}/participants")]
    public async Task<ActionResult<List<StudyRoomParticipantDto>>> GetRoomParticipants(Guid id)
    {
        try
        {
            var participants = await _roomService.GetRoomParticipantsAsync(id);
            return Ok(participants);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting participants for room {id}");
            return StatusCode(500, "Error retrieving participants");
        }
    }

    /// <summary>
    /// Check if user can join a room
    /// </summary>
    [HttpPost("{id}/can-join")]
    public async Task<ActionResult<CanJoinRoomResponse>> CanJoinRoom(Guid id, [FromBody] CheckAccessRequest? request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User not authenticated");
            }

            var canJoin = await _roomService.CanJoinRoomAsync(id, userId, request?.RoomCode);

            return Ok(new CanJoinRoomResponse
            {
                CanJoin = canJoin,
                Message = canJoin ? "You can join this room" : "Unable to join room"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error checking room access {id}");
            return StatusCode(500, "Error checking room access");
        }
    }

    #region Helper Methods

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

    #endregion
}

// Supporting DTOs
public class UpdateRoomStatusRequest
{
    public string Status { get; set; } = string.Empty;
}

public class CheckAccessRequest
{
    public string? RoomCode { get; set; }
}

public class CanJoinRoomResponse
{
    public bool CanJoin { get; set; }
    public string? Message { get; set; }
}

