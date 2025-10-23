using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using Tutorly.Server.Hubs;
using Tutorly.Server.Services;
using Tutorly.Shared;

namespace Tutorly.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MessagingController : ControllerBase
    {
        private readonly IMessagingService _messagingService;
        private readonly ILogger<MessagingController> _logger;
        private readonly IHubContext<MessagingHub> _messagingHub;

        public MessagingController(IMessagingService messagingService, ILogger<MessagingController> logger, IHubContext<MessagingHub> messagingHub)
        {
            _messagingService = messagingService;
            _logger = logger;
            _messagingHub = messagingHub;
        }

        #region Conversation Endpoints

        /// <summary>
        /// Get all conversations for the current user
        /// </summary>
        [HttpGet("conversations")]
        public async Task<ActionResult<ApiResponse<List<ConversationDto>>>> GetConversations([FromQuery] ConversationSearchDto filter)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse<List<ConversationDto>> { Success = false, Message = "User not authenticated" });

                var result = await _messagingService.GetConversationsAsync(filter, userId);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting conversations");
                return StatusCode(500, new ApiResponse<List<ConversationDto>> { Success = false, Message = "Internal server error" });
            }
        }

        /// <summary>
        /// Create a direct conversation with another user
        /// </summary>
        [HttpPost("conversations/direct")]
        public async Task<ActionResult<ApiResponse<ConversationDto>>> CreateDirectConversation([FromBody] CreateConversationDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse<ConversationDto> { Success = false, Message = "User not authenticated" });

                if (string.IsNullOrEmpty(dto.OtherUserId))
                    return BadRequest(new ApiResponse<ConversationDto> { Success = false, Message = "Other user ID is required" });

                var result = await _messagingService.CreateDirectConversationAsync(dto.OtherUserId, userId, dto.InitialMessage);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating direct conversation");
                return StatusCode(500, new ApiResponse<ConversationDto> { Success = false, Message = "Internal server error" });
            }
        }

        /// <summary>
        /// Create a group conversation
        /// </summary>
        [HttpPost("conversations/group")]
        public async Task<ActionResult<ApiResponse<ConversationDto>>> CreateGroupConversation([FromBody] CreateConversationDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse<ConversationDto> { Success = false, Message = "User not authenticated" });

                var result = await _messagingService.CreateGroupConversationAsync(dto, userId);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating group conversation");
                return StatusCode(500, new ApiResponse<ConversationDto> { Success = false, Message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get a specific conversation by ID
        /// </summary>
        [HttpGet("conversations/{conversationId}")]
        public async Task<ActionResult<ApiResponse<ConversationDto>>> GetConversation(int conversationId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse<ConversationDto> { Success = false, Message = "User not authenticated" });

                var result = await _messagingService.GetConversationByIdAsync(conversationId, userId);

                if (!result.Success)
                    return NotFound(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting conversation");
                return StatusCode(500, new ApiResponse<ConversationDto> { Success = false, Message = "Internal server error" });
            }
        }

        /// <summary>
        /// Update group conversation details
        /// </summary>
        [HttpPut("conversations/{conversationId}")]
        public async Task<ActionResult<ApiResponse<ConversationDto>>> UpdateGroupDetails(int conversationId, [FromBody] UpdateGroupDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse<ConversationDto> { Success = false, Message = "User not authenticated" });

                var result = await _messagingService.UpdateGroupDetailsAsync(conversationId, dto, userId);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating group details");
                return StatusCode(500, new ApiResponse<ConversationDto> { Success = false, Message = "Internal server error" });
            }
        }

        /// <summary>
        /// Leave a conversation
        /// </summary>
        [HttpPost("conversations/{conversationId}/leave")]
        public async Task<ActionResult<ApiResponse<bool>>> LeaveConversation(int conversationId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse<bool> { Success = false, Message = "User not authenticated" });

                var result = await _messagingService.LeaveConversationAsync(conversationId, userId);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leaving conversation");
                return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Internal server error" });
            }
        }

        /// <summary>
        /// Delete a conversation
        /// </summary>
        [HttpDelete("conversations/{conversationId}")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteConversation(int conversationId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse<bool> { Success = false, Message = "User not authenticated" });

                var result = await _messagingService.DeleteConversationAsync(conversationId, userId);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting conversation");
                return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Internal server error" });
            }
        }

        /// <summary>
        /// Mute conversation notifications
        /// </summary>
        [HttpPost("conversations/{conversationId}/mute")]
        public async Task<ActionResult<ApiResponse<bool>>> MuteConversation(int conversationId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse<bool> { Success = false, Message = "User not authenticated" });

                var result = await _messagingService.MuteConversationAsync(conversationId, userId);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error muting conversation");
                return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Internal server error" });
            }
        }

        /// <summary>
        /// Unmute conversation notifications
        /// </summary>
        [HttpPost("conversations/{conversationId}/unmute")]
        public async Task<ActionResult<ApiResponse<bool>>> UnmuteConversation(int conversationId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse<bool> { Success = false, Message = "User not authenticated" });

                var result = await _messagingService.UnmuteConversationAsync(conversationId, userId);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unmuting conversation");
                return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Internal server error" });
            }
        }

        #endregion

        #region Group Management Endpoints

        /// <summary>
        /// Get participants in a conversation
        /// </summary>
        [HttpGet("conversations/{conversationId}/participants")]
        public async Task<ActionResult<ApiResponse<List<ParticipantDto>>>> GetParticipants(int conversationId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse<List<ParticipantDto>> { Success = false, Message = "User not authenticated" });

                var result = await _messagingService.GetParticipantsAsync(conversationId, userId);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting participants");
                return StatusCode(500, new ApiResponse<List<ParticipantDto>> { Success = false, Message = "Internal server error" });
            }
        }

        /// <summary>
        /// Add participants to a group
        /// </summary>
        [HttpPost("conversations/{conversationId}/participants")]
        public async Task<ActionResult<ApiResponse<bool>>> AddParticipants(int conversationId, [FromBody] AddParticipantsDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse<bool> { Success = false, Message = "User not authenticated" });

                var result = await _messagingService.AddParticipantsAsync(conversationId, dto, userId);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding participants");
                return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Internal server error" });
            }
        }

        /// <summary>
        /// Remove a participant from a group
        /// </summary>
        [HttpDelete("conversations/{conversationId}/participants/{participantUserId}")]
        public async Task<ActionResult<ApiResponse<bool>>> RemoveParticipant(int conversationId, string participantUserId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse<bool> { Success = false, Message = "User not authenticated" });

                var result = await _messagingService.RemoveParticipantAsync(conversationId, participantUserId, userId);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing participant");
                return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Internal server error" });
            }
        }

        /// <summary>
        /// Update a participant's role
        /// </summary>
        [HttpPut("conversations/{conversationId}/participants/{participantUserId}/role")]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateParticipantRole(int conversationId, string participantUserId, [FromBody] UpdateParticipantRoleDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse<bool> { Success = false, Message = "User not authenticated" });

                var result = await _messagingService.UpdateParticipantRoleAsync(conversationId, participantUserId, dto, userId);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating participant role");
                return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Internal server error" });
            }
        }

        /// <summary>
        /// Update user's nickname in a group
        /// </summary>
        [HttpPut("conversations/{conversationId}/nickname")]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateNickname(int conversationId, [FromBody] string nickname)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse<bool> { Success = false, Message = "User not authenticated" });

                var result = await _messagingService.UpdateNicknameAsync(conversationId, nickname, userId);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating nickname");
                return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Internal server error" });
            }
        }

        /// <summary>
        /// Invite users to a group
        /// </summary>
        [HttpPost("conversations/{conversationId}/invite")]
        public async Task<ActionResult<ApiResponse<bool>>> InviteToGroup(int conversationId, [FromBody] InviteUsersDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse<bool> { Success = false, Message = "User not authenticated" });

                var result = await _messagingService.InviteToGroupAsync(conversationId, dto, userId);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending invitations");
                return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get pending invitations for current user
        /// </summary>
        [HttpGet("invitations")]
        public async Task<ActionResult<ApiResponse<List<GroupInvitationDto>>>> GetPendingInvitations()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse<List<GroupInvitationDto>> { Success = false, Message = "User not authenticated" });

                var result = await _messagingService.GetPendingInvitationsAsync(userId);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting invitations");
                return StatusCode(500, new ApiResponse<List<GroupInvitationDto>> { Success = false, Message = "Internal server error" });
            }
        }

        /// <summary>
        /// Accept a group invitation
        /// </summary>
        [HttpPost("invitations/{invitationId}/accept")]
        public async Task<ActionResult<ApiResponse<bool>>> AcceptInvitation(int invitationId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse<bool> { Success = false, Message = "User not authenticated" });

                var result = await _messagingService.RespondToInvitationAsync(invitationId, true, userId);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting invitation");
                return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Internal server error" });
            }
        }

        /// <summary>
        /// Decline a group invitation
        /// </summary>
        [HttpPost("invitations/{invitationId}/decline")]
        public async Task<ActionResult<ApiResponse<bool>>> DeclineInvitation(int invitationId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse<bool> { Success = false, Message = "User not authenticated" });

                var result = await _messagingService.RespondToInvitationAsync(invitationId, false, userId);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error declining invitation");
                return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Internal server error" });
            }
        }

        #endregion

        #region Message Endpoints

        /// <summary>
        /// Get messages in a conversation
        /// </summary>
        [HttpGet("conversations/{conversationId}/messages")]
        public async Task<ActionResult<ApiResponse<List<MessageDto>>>> GetMessages(int conversationId, [FromQuery] MessageFilterDto filter)
        {
            try
            {
                _logger.LogInformation($"[GetMessages] ✓ Endpoint called - ConversationId: {conversationId}, Limit: {filter.Limit}");

                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning($"[GetMessages] ✗ User not authenticated for conversation {conversationId}");
                    return Unauthorized(new ApiResponse<List<MessageDto>> { Success = false, Message = "User not authenticated" });
                }

                _logger.LogInformation($"[GetMessages] UserId: {userId}, calling service...");
                var result = await _messagingService.GetMessagesAsync(conversationId, filter, userId);

                _logger.LogInformation($"[GetMessages] Service returned - Success: {result.Success}, Count: {result.Data?.Count ?? 0}, Message: {result.Message}");

                if (!result.Success)
                {
                    _logger.LogWarning($"[GetMessages] Service failed: {result.Message}");
                    return BadRequest(result);
                }

                _logger.LogInformation($"[GetMessages] ✓ Returning {result.Data?.Count ?? 0} messages for conversation {conversationId}");
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[GetMessages] ✗ ERROR getting messages for conversation {conversationId}");
                return StatusCode(500, new ApiResponse<List<MessageDto>> { Success = false, Message = "Internal server error" });
            }
        }

        /// <summary>
        /// Send a message in a conversation
        /// </summary>
        [HttpPost("conversations/{conversationId}/messages")]
        public async Task<ActionResult<ApiResponse<MessageDto>>> SendMessage(int conversationId, [FromBody] SendMessageDto dto)
        {
            try
            {
                _logger.LogInformation($"[SendMessage] ✓ Endpoint called - ConversationId: {conversationId}, Content: '{dto.Content?.Substring(0, Math.Min(30, dto.Content?.Length ?? 0))}...'");

                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning($"[SendMessage] ✗ User not authenticated");
                    return Unauthorized(new ApiResponse<MessageDto> { Success = false, Message = "User not authenticated" });
                }

                _logger.LogInformation($"[SendMessage] UserId: {userId}, calling service to save to database...");
                var result = await _messagingService.SendMessageAsync(conversationId, dto, userId);

                if (!result.Success)
                {
                    _logger.LogWarning($"[SendMessage] ✗ Service returned failure: {result.Message}");
                    return BadRequest(result);
                }

                _logger.LogInformation($"[SendMessage] ✓ Message saved to database - MessageId: {result.Data?.MessageId}");

                // Broadcast to all participants via SignalR
                // NOTE: Group name must match MessagingHub.GetConversationGroupName() format: "Conversation_{id}" with capital C
                try
                {
                    var groupName = $"Conversation_{conversationId}";  // Capital 'C' to match hub
                    await _messagingHub.Clients.Group(groupName)
                        .SendAsync("ReceiveMessage", result.Data);
                    _logger.LogInformation($"[SendMessage] ✓ Broadcasted message {result.Data?.MessageId} via SignalR to {groupName}");
                }
                catch (Exception signalREx)
                {
                    _logger.LogError(signalREx, $"[SendMessage] ✗ Failed to broadcast via SignalR (message was saved though)");
                    // Don't fail the request - message is saved, SignalR is just for real-time updates
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SendMessage] ✗ ERROR sending message");
                return StatusCode(500, new ApiResponse<MessageDto> { Success = false, Message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get a specific message by ID
        /// </summary>
        [HttpGet("messages/{messageId}")]
        public async Task<ActionResult<ApiResponse<MessageDto>>> GetMessage(int messageId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse<MessageDto> { Success = false, Message = "User not authenticated" });

                var result = await _messagingService.GetMessageByIdAsync(messageId, userId);

                if (!result.Success)
                    return NotFound(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting message");
                return StatusCode(500, new ApiResponse<MessageDto> { Success = false, Message = "Internal server error" });
            }
        }

        /// <summary>
        /// Edit a message
        /// </summary>
        [HttpPut("messages/{messageId}")]
        public async Task<ActionResult<ApiResponse<MessageDto>>> EditMessage(int messageId, [FromBody] EditMessageDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse<MessageDto> { Success = false, Message = "User not authenticated" });

                var result = await _messagingService.EditMessageAsync(messageId, dto, userId);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing message");
                return StatusCode(500, new ApiResponse<MessageDto> { Success = false, Message = "Internal server error" });
            }
        }

        /// <summary>
        /// Delete a message
        /// </summary>
        [HttpDelete("messages/{messageId}")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteMessage(int messageId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse<bool> { Success = false, Message = "User not authenticated" });

                var result = await _messagingService.DeleteMessageAsync(messageId, userId);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message");
                return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Internal server error" });
            }
        }

        /// <summary>
        /// Pin a message
        /// </summary>
        [HttpPost("messages/{messageId}/pin")]
        public async Task<ActionResult<ApiResponse<bool>>> PinMessage(int messageId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse<bool> { Success = false, Message = "User not authenticated" });

                var result = await _messagingService.PinMessageAsync(messageId, userId);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pinning message");
                return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Internal server error" });
            }
        }

        /// <summary>
        /// Unpin a message
        /// </summary>
        [HttpDelete("messages/{messageId}/pin")]
        public async Task<ActionResult<ApiResponse<bool>>> UnpinMessage(int messageId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse<bool> { Success = false, Message = "User not authenticated" });

                var result = await _messagingService.UnpinMessageAsync(messageId, userId);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unpinning message");
                return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get pinned messages in a conversation
        /// </summary>
        [HttpGet("conversations/{conversationId}/pinned-messages")]
        public async Task<ActionResult<ApiResponse<List<MessageDto>>>> GetPinnedMessages(int conversationId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse<List<MessageDto>> { Success = false, Message = "User not authenticated" });

                var result = await _messagingService.GetPinnedMessagesAsync(conversationId, userId);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pinned messages");
                return StatusCode(500, new ApiResponse<List<MessageDto>> { Success = false, Message = "Internal server error" });
            }
        }

        /// <summary>
        /// Mark messages as read
        /// </summary>
        [HttpPost("conversations/{conversationId}/read")]
        public async Task<ActionResult<ApiResponse<bool>>> MarkMessagesAsRead(int conversationId, [FromBody] MarkMessagesAsReadDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse<bool> { Success = false, Message = "User not authenticated" });

                var result = await _messagingService.MarkMessagesAsReadAsync(conversationId, dto, userId);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as read");
                return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Internal server error" });
            }
        }

        /// <summary>
        /// Search messages in a conversation
        /// </summary>
        [HttpGet("conversations/{conversationId}/search")]
        public async Task<ActionResult<ApiResponse<List<MessageDto>>>> SearchMessages(int conversationId, [FromQuery] string query)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse<List<MessageDto>> { Success = false, Message = "User not authenticated" });

                if (string.IsNullOrWhiteSpace(query))
                    return BadRequest(new ApiResponse<List<MessageDto>> { Success = false, Message = "Search query is required" });

                var result = await _messagingService.SearchMessagesAsync(conversationId, query, userId);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching messages");
                return StatusCode(500, new ApiResponse<List<MessageDto>> { Success = false, Message = "Internal server error" });
            }
        }

        #endregion

        #region User Search & Presence Endpoints

        /// <summary>
        /// Search for users to message
        /// </summary>
        [HttpGet("users/search")]
        public async Task<ActionResult<ApiResponse<List<UserSearchResultDto>>>> SearchUsers([FromQuery] UserSearchDto dto)
        {
            try
            {
                _logger.LogInformation($"[SearchUsers] Received request - Query: '{dto.SearchQuery}', RoleFilter: '{dto.RoleFilter}', PageSize: {dto.PageSize}");

                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("[SearchUsers] User not authenticated");
                    return Unauthorized(new ApiResponse<List<UserSearchResultDto>> { Success = false, Message = "User not authenticated" });
                }

                _logger.LogInformation($"[SearchUsers] Current UserId: {userId}");

                var result = await _messagingService.SearchUsersAsync(dto, userId);

                _logger.LogInformation($"[SearchUsers] Service returned - Success: {result.Success}, Count: {result.Data?.Count ?? 0}");

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SearchUsers] Error searching users");
                return StatusCode(500, new ApiResponse<List<UserSearchResultDto>> { Success = false, Message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get a user's presence status
        /// </summary>
        [HttpGet("presence/{userId}")]
        public async Task<ActionResult<ApiResponse<PresenceDto>>> GetUserPresence(string userId)
        {
            try
            {
                var result = await _messagingService.GetUserPresenceAsync(userId);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting presence");
                return StatusCode(500, new ApiResponse<PresenceDto> { Success = false, Message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get presence for multiple users
        /// </summary>
        [HttpGet("presence/multiple")]
        public async Task<ActionResult<ApiResponse<List<PresenceDto>>>> GetMultiplePresences([FromQuery] List<string> userIds)
        {
            try
            {
                if (userIds == null || !userIds.Any())
                    return BadRequest(new ApiResponse<List<PresenceDto>> { Success = false, Message = "User IDs are required" });

                var result = await _messagingService.GetMultiplePresencesAsync(userIds);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting multiple presences");
                return StatusCode(500, new ApiResponse<List<PresenceDto>> { Success = false, Message = "Internal server error" });
            }
        }

        #endregion

        #region Statistics Endpoints

        /// <summary>
        /// Get total unread message count
        /// </summary>
        [HttpGet("unread-count")]
        public async Task<ActionResult<ApiResponse<int>>> GetTotalUnreadCount()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse<int> { Success = false, Message = "User not authenticated" });

                var result = await _messagingService.GetTotalUnreadCountAsync(userId);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread count");
                return StatusCode(500, new ApiResponse<int> { Success = false, Message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get unread counts for all conversations
        /// </summary>
        [HttpGet("unread-counts/all")]
        public async Task<ActionResult<ApiResponse<AllUnreadCountsDto>>> GetAllUnreadCounts()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse<AllUnreadCountsDto> { Success = false, Message = "User not authenticated" });

                var result = await _messagingService.GetUnreadCountsForAllConversationsAsync(userId);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all unread counts");
                return StatusCode(500, new ApiResponse<AllUnreadCountsDto> { Success = false, Message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get unread count for a specific conversation
        /// </summary>
        [HttpGet("conversations/{conversationId}/unread-count")]
        public async Task<ActionResult<ApiResponse<int>>> GetConversationUnreadCount(int conversationId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse<int> { Success = false, Message = "User not authenticated" });

                var result = await _messagingService.GetConversationUnreadCountAsync(conversationId, userId);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting conversation unread count");
                return StatusCode(500, new ApiResponse<int> { Success = false, Message = "Internal server error" });
            }
        }

        #endregion

        #region Helper Methods

        private string? GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }

        #endregion
    }
}
