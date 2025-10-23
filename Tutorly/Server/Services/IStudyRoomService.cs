using Tutorly.Shared;

namespace Tutorly.Server.Services;

public interface IStudyRoomService
{
    Task<CreateStudyRoomResponse> CreateRoomAsync(CreateStudyRoomRequest request, Guid currentUserId);
    Task<StudyRoomDto?> GetRoomByIdAsync(Guid roomId);
    Task<List<StudyRoomDto>> GetAvailableRoomsAsync(StudyRoomFilters filters, Guid currentUserId);
    Task<List<StudyRoomDto>> GetMySessionsAsync(Guid currentUserId);
    Task<JoinRoomResponse> JoinRoomAsync(JoinRoomRequest request, Guid currentUserId, string connectionId);
    Task<bool> LeaveRoomAsync(Guid roomId, Guid userId);
    Task<bool> UpdateRoomStatusAsync(Guid roomId, string status);
    Task<bool> UpdateParticipantStatusAsync(Guid roomId, Guid userId, bool? isMuted, bool? isScreenSharing, bool? isVideoEnabled = null);
    Task<List<StudyRoomParticipantDto>> GetRoomParticipantsAsync(Guid roomId);
    Task<bool> DeleteRoomAsync(Guid roomId, Guid currentUserId);
    Task<bool> DeleteRoomAsync(Guid roomId);
    Task<bool> CanJoinRoomAsync(Guid roomId, Guid userId, string? roomCode);
    Task<bool> CleanupEmptyRoomAsync(Guid roomId);
    Task CleanupAllEmptyRoomsAsync();
    Task CleanupInactiveSingleUserRoomsAsync();

    // Chat-related methods
    Task<RoomChatMessageDto> SaveChatMessageAsync(Guid roomId, Guid userId, string message);
    Task<List<RoomChatMessageDto>> GetRoomChatHistoryAsync(Guid roomId);
    Task<int> CreateOrGetRoomConversationAsync(Guid roomId, string roomName);
}

