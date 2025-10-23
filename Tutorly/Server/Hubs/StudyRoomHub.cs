using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using Tutorly.Server.Services;
using Tutorly.Server.Helpers;
using Tutorly.Server.DatabaseModels;
using Tutorly.Shared;
using Supabase;

namespace Tutorly.Server.Hubs;

[Authorize]
public class StudyRoomHub : Hub
{
    private readonly IStudyRoomService _roomService;
    private readonly ILogger<StudyRoomHub> _logger;
    private readonly IMessagingService _messagingService;
    private readonly ISupabaseClientFactory _supabaseFactory;

    public StudyRoomHub(IStudyRoomService roomService, ILogger<StudyRoomHub> logger, IMessagingService messagingService, ISupabaseClientFactory supabaseFactory)
    {
        _roomService = roomService;
        _logger = logger;
        _messagingService = messagingService;
        _supabaseFactory = supabaseFactory;
    }

    #region Connection Lifecycle

    public override async Task OnConnectedAsync()
    {
        try
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("User connected without valid user ID");
                Console.WriteLine($"❌ StudyRoomHub: User connected without valid user ID - Connection: {Context.ConnectionId}");
                return;
            }

            _logger.LogInformation($"User {userId} connected to StudyRoomHub with connection {Context.ConnectionId}");
            Console.WriteLine($"✅ StudyRoomHub: User {userId} connected with connection {Context.ConnectionId}");

            // Log all available claims for debugging
            if (Context.User?.Claims != null)
            {
                var claims = string.Join(", ", Context.User.Claims.Select(c => $"{c.Type}={c.Value}"));
                Console.WriteLine($"🔍 StudyRoomHub: User claims: {claims}");
            }

            await base.OnConnectedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnConnectedAsync");
            Console.WriteLine($"❌ StudyRoomHub OnConnectedAsync error: {ex.Message}");
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            var userId = GetUserId();
            if (!string.IsNullOrEmpty(userId))
            {
                _logger.LogInformation($"User {userId} disconnected from StudyRoomHub");

                // Find and leave any active rooms for this user
                // This is handled by the client calling LeaveRoom explicitly
            }

            await base.OnDisconnectedAsync(exception);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnDisconnectedAsync");
        }
    }

    #endregion

    #region Room Management

    /// <summary>
    /// Join a study room
    /// </summary>
    public async Task<JoinRoomResponse> JoinRoom(Guid roomId, string? roomCode = null)
    {
        try
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return new JoinRoomResponse
                {
                    Success = false,
                    Message = "User not authenticated"
                };
            }

            var userGuid = Guid.Parse(userId);
            var request = new JoinRoomRequest
            {
                RoomId = roomId,
                RoomCode = roomCode
            };

            var response = await _roomService.JoinRoomAsync(request, userGuid, Context.ConnectionId);

            if (response.Success)
            {
                // Add user to SignalR group
                var groupName = GetRoomGroupName(roomId);
                await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

                // Notify other participants
                var participant = response.CurrentParticipants.FirstOrDefault(p => p.UserId == userGuid);
                if (participant != null)
                {
                    await Clients.OthersInGroup(groupName).SendAsync("ParticipantJoined", participant);
                }

                _logger.LogInformation($"User {userId} joined room {roomId}");
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error joining room {roomId}");
            return new JoinRoomResponse
            {
                Success = false,
                Message = $"Error joining room: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Leave a study room
    /// </summary>
    public async Task LeaveRoom(Guid roomId)
    {
        try
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
                return;

            var userGuid = Guid.Parse(userId);
            var groupName = GetRoomGroupName(roomId);

            await _roomService.LeaveRoomAsync(roomId, userGuid);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

            // Notify other participants
            await Clients.OthersInGroup(groupName).SendAsync("ParticipantLeft", userGuid);

            // Check if room should be cleaned up (no active participants)
            var wasCleanedUp = await _roomService.CleanupEmptyRoomAsync(roomId);
            if (wasCleanedUp)
            {
                _logger.LogInformation($"Room {roomId} was cleaned up - sending RoomEnded event");
                await Clients.Group(groupName).SendAsync("RoomEnded", "Room ended - all participants left");
            }
            else
            {
                _logger.LogInformation($"Room {roomId} still has participants - not cleaning up");
            }

            _logger.LogInformation($"User {userId} left room {roomId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error leaving room {roomId}");
        }
    }

    #endregion

    #region WebRTC Signaling

    /// <summary>
    /// Send WebRTC offer to another peer
    /// </summary>
    public async Task SendOffer(Guid roomId, Guid targetUserId, string sdp)
    {
        try
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
                return;

            var fromUserId = Guid.Parse(userId);

            var signal = new WebRTCSignalDto
            {
                SignalType = "Offer",
                RoomId = roomId,
                FromUserId = fromUserId,
                ToUserId = targetUserId,
                Sdp = sdp
            };

            // Find target user's connection and send offer
            await Clients.Group(GetRoomGroupName(roomId))
                .SendAsync("ReceiveOffer", signal);

            _logger.LogInformation($"Offer sent from {userId} to {targetUserId} in room {roomId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending WebRTC offer");
        }
    }

    /// <summary>
    /// Send WebRTC answer to another peer
    /// </summary>
    public async Task SendAnswer(Guid roomId, Guid targetUserId, string sdp)
    {
        try
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
                return;

            var fromUserId = Guid.Parse(userId);

            var signal = new WebRTCSignalDto
            {
                SignalType = "Answer",
                RoomId = roomId,
                FromUserId = fromUserId,
                ToUserId = targetUserId,
                Sdp = sdp
            };

            await Clients.Group(GetRoomGroupName(roomId))
                .SendAsync("ReceiveAnswer", signal);

            _logger.LogInformation($"Answer sent from {userId} to {targetUserId} in room {roomId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending WebRTC answer");
        }
    }

    /// <summary>
    /// Send ICE candidate to another peer
    /// </summary>
    public async Task SendIceCandidate(Guid roomId, Guid targetUserId, string candidate, string? sdpMid, int? sdpMLineIndex)
    {
        try
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
                return;

            var fromUserId = Guid.Parse(userId);

            var signal = new WebRTCSignalDto
            {
                SignalType = "IceCandidate",
                RoomId = roomId,
                FromUserId = fromUserId,
                ToUserId = targetUserId,
                Candidate = candidate,
                SdpMid = sdpMid,
                SdpMLineIndex = sdpMLineIndex
            };

            await Clients.Group(GetRoomGroupName(roomId))
                .SendAsync("ReceiveIceCandidate", signal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending ICE candidate");
        }
    }


    #endregion

    #region Room Chat

    /// <summary>
    /// Send chat message in room
    /// </summary>
    public async Task SendChatMessage(Guid roomId, string message)
    {
        try
        {
            _logger.LogInformation($"💬 SendChatMessage received: roomId={roomId}, message='{message}'");

            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("💬 SendChatMessage: No user ID found");
                return;
            }

            _logger.LogInformation($"💬 SendChatMessage: User {userId} sending message to room {roomId}");

            // Use StudyRoomService to save message (which integrates with messaging system)
            var savedMessage = await _roomService.SaveChatMessageAsync(
                roomId,
                Guid.Parse(userId),
                message
            );

            _logger.LogInformation($"💬 SendChatMessage: Message saved successfully, broadcasting to group");

            // Broadcast to study room group using the existing pattern
            var groupName = GetRoomGroupName(roomId);
            await Clients.Group(groupName).SendAsync("ReceiveChatMessage", savedMessage);

            _logger.LogInformation($"✅ Chat message sent in room {roomId} by user {userId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error sending chat message");
        }
    }

    /// <summary>
    /// Get chat history for a room
    /// </summary>
    public async Task<List<RoomChatMessageDto>> GetRoomChatHistory(Guid roomId)
    {
        try
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
                return new List<RoomChatMessageDto>();

            // Verify user is participant in room
            var participants = await _roomService.GetRoomParticipantsAsync(roomId);
            if (!participants.Any(p => p.UserId == Guid.Parse(userId)))
            {
                _logger.LogWarning($"User {userId} attempted to get chat history for room {roomId} without being a participant");
                return new List<RoomChatMessageDto>();
            }

            return await _roomService.GetRoomChatHistoryAsync(roomId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting chat history for room {roomId}");
            return new List<RoomChatMessageDto>();
        }
    }

    #endregion

    #region Participant Status

    /// <summary>
    /// Toggle mute status
    /// </summary>
    public async Task ToggleMute(Guid roomId, bool isMuted)
    {
        try
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
                return;

            var userGuid = Guid.Parse(userId);
            await _roomService.UpdateParticipantStatusAsync(roomId, userGuid, isMuted, null);

            var groupName = GetRoomGroupName(roomId);
            await Clients.OthersInGroup(groupName).SendAsync("ParticipantMuteChanged", userGuid, isMuted);

            _logger.LogInformation($"User {userId} mute status: {isMuted} in room {roomId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling mute");
        }
    }

    /// <summary>
    /// Toggle screen share status
    /// </summary>
    public async Task ToggleScreenShare(Guid roomId, bool isSharing)
    {
        try
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
                return;

            var userGuid = Guid.Parse(userId);
            await _roomService.UpdateParticipantStatusAsync(roomId, userGuid, null, isSharing);

            var groupName = GetRoomGroupName(roomId);
            await Clients.OthersInGroup(groupName).SendAsync("ParticipantScreenShareChanged", userGuid, isSharing);

            _logger.LogInformation($"User {userId} screen share status: {isSharing} in room {roomId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling screen share");
        }
    }

    /// <summary>
    /// Update video status (camera on/off)
    /// </summary>
    public async Task ToggleVideo(Guid roomId, bool isVideoEnabled)
    {
        try
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
                return;

            var userGuid = Guid.Parse(userId);
            await _roomService.UpdateParticipantStatusAsync(roomId, userGuid, null, null, isVideoEnabled);

            var groupName = GetRoomGroupName(roomId);
            await Clients.OthersInGroup(groupName).SendAsync("ParticipantVideoChanged", userGuid, isVideoEnabled);

            _logger.LogInformation($"User {userId} video status: {isVideoEnabled} in room {roomId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling video");
        }
    }

    #endregion

    #region Call Management

    /// <summary>
    /// Initiate a call to another user
    /// </summary>
    public async Task InitiateCall(int conversationId, Guid toUserId, CallType callType)
    {
        try
        {
            var fromUserId = GetUserId();
            if (string.IsNullOrEmpty(fromUserId))
            {
                Console.WriteLine("❌ InitiateCall: No user ID found");
                return;
            }

            Console.WriteLine($"📞 InitiateCall: From {fromUserId} to {toUserId} (Type: {callType})");

            // Create study room for the call
            var roomName = callType == CallType.Video ? "Video Call" : "Voice Call";
            var request = new CreateStudyRoomRequest
            {
                RoomName = roomName,
                Description = "Direct call room",
                Privacy = "PrivateInviteOnly",
                RoomType = "Standalone"
            };
            var response = await _roomService.CreateRoomAsync(request, Guid.Parse(fromUserId));
            if (!response.Success || response.Room == null)
            {
                _logger.LogError("Failed to create room for call");
                Console.WriteLine($"❌ InitiateCall: Failed to create room - {response.Message}");
                return;
            }
            var room = response.Room;

            var fromUserName = await GetUserNameAsync(fromUserId);
            var callInvitation = new CallInvitationDto
            {
                CallId = Guid.NewGuid(),
                RoomId = room.RoomId,
                FromUserId = Guid.Parse(fromUserId),
                FromUserName = fromUserName,
                ToUserId = toUserId,
                CallType = callType,
                CreatedAt = DateTime.UtcNow
            };

            Console.WriteLine($"📞 InitiateCall: Created invitation - CallId: {callInvitation.CallId}, From: {fromUserName}, Room: {room.RoomId}");

            // Send to specific user
            _logger.LogInformation($"Attempting to send call invitation to user: {toUserId}");
            Console.WriteLine($"📤 InitiateCall: Sending to user {toUserId}...");

            // Check if the target user is connected
            var targetUserConnections = Context.GetHttpContext()?.RequestServices
                ?.GetService<IHubContext<StudyRoomHub>>()?.Clients?.User(toUserId.ToString());

            Console.WriteLine($"📤 InitiateCall: Target user connection status: {targetUserConnections != null}");

            await Clients.User(toUserId.ToString())
                .SendAsync("ReceiveCallInvitation", callInvitation);

            Console.WriteLine($"✅ InitiateCall: Call invitation sent successfully from {fromUserId} to {toUserId}");
            _logger.LogInformation($"Call initiated from {fromUserId} to {toUserId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating call");
            Console.WriteLine($"❌ InitiateCall error: {ex.Message}");
        }
    }

    /// <summary>
    /// Respond to a call invitation
    /// </summary>
    public async Task RespondToCall(CallResponseDto response)
    {
        try
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return;

            if (response.Accepted)
            {
                // Notify caller that call was accepted
                var room = await _roomService.GetRoomByIdAsync(response.RoomId);
                if (room != null)
                {
                    await Clients.User(room.CreatorUserId.ToString())
                        .SendAsync("CallAccepted", response);
                }
            }
            else
            {
                // Notify caller that call was rejected, then delete room
                var room = await _roomService.GetRoomByIdAsync(response.RoomId);
                if (room != null)
                {
                    // Send rejection notification first
                    await Clients.User(room.CreatorUserId.ToString())
                        .SendAsync("CallRejected", response);

                    // Small delay to ensure notification is sent before room deletion
                    await Task.Delay(100);
                }

                // Delete room after notification is sent
                await _roomService.DeleteRoomAsync(response.RoomId);
            }

            _logger.LogInformation($"Call response: {(response.Accepted ? "Accepted" : "Rejected")}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error responding to call");
        }
    }

    /// <summary>
    /// Cancel an ongoing call
    /// </summary>
    public async Task CancelCall(Guid callId, Guid roomId)
    {
        try
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return;

            // Delete the room
            await _roomService.DeleteRoomAsync(roomId);

            // Notify the other participant
            var participants = await _roomService.GetRoomParticipantsAsync(roomId);
            var otherUser = participants.FirstOrDefault(p => p.UserId.ToString() != userId);

            if (otherUser != null)
            {
                await Clients.User(otherUser.UserId.ToString())
                    .SendAsync("CallCancelled", callId);
            }

            _logger.LogInformation($"Call {callId} cancelled by {userId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling call");
        }
    }

    #endregion

    #region Helper Methods

    private string GetUserId()
    {
        return Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value
            ?? string.Empty;
    }

    private string GetRoomGroupName(Guid roomId)
    {
        return $"studyroom_{roomId}";
    }

    private async Task<string> GetUserNameAsync(string userId)
    {
        try
        {
            // Try to get user name from the database
            var client = _supabaseFactory.CreateService();
            var response = await client
                .From<StudentProfileEntity>()
                .Select("full_name")
                .Where(x => x.UserId == userId)
                .Single();

            if (response != null && !string.IsNullOrEmpty(response.FullName))
            {
                Console.WriteLine($"✅ GetUserNameAsync: Found user {userId} -> {response.FullName}");
                return response.FullName;
            }

            // Fallback: try tutor profile
            var tutorResponse = await client
                .From<TutorProfileEntity>()
                .Select("full_name")
                .Where(x => x.UserId == userId)
                .Single();

            if (tutorResponse != null && !string.IsNullOrEmpty(tutorResponse.FullName))
            {
                Console.WriteLine($"✅ GetUserNameAsync: Found tutor {userId} -> {tutorResponse.FullName}");
                return tutorResponse.FullName;
            }

            // Fallback: try admin profile
            var adminResponse = await client
                .From<AdminProfileEntity>()
                .Select("full_name")
                .Where(x => x.UserId == userId)
                .Single();

            if (adminResponse != null && !string.IsNullOrEmpty(adminResponse.FullName))
            {
                Console.WriteLine($"✅ GetUserNameAsync: Found admin {userId} -> {adminResponse.FullName}");
                return adminResponse.FullName;
            }

            Console.WriteLine($"⚠️ GetUserNameAsync: No profile found for user {userId}, using fallback");
            return "User";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting user name for {userId}");
            Console.WriteLine($"❌ GetUserNameAsync error for {userId}: {ex.Message}");
            return "Unknown User";
        }
    }

    #endregion
}

