namespace Tutorly.Shared;

public class StudyRoomDto
{
    public Guid RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CreatorUserId { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ScheduledStartTime { get; set; }
    public DateTime? ScheduledEndTime { get; set; }
    public string RoomType { get; set; } = "Standalone";
    public string Privacy { get; set; } = "Public";
    public Guid? ModuleId { get; set; }
    public string? ModuleName { get; set; }
    public int MaxParticipants { get; set; } = 10;
    public string Status { get; set; } = "Scheduled";
    public string? RoomCode { get; set; }
    public int CurrentParticipantCount { get; set; }
    public List<StudyRoomParticipantDto> Participants { get; set; } = new();
}

public class CreateStudyRoomRequest
{
    public string RoomName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? ScheduledStartTime { get; set; }
    public DateTime? ScheduledEndTime { get; set; }
    public string RoomType { get; set; } = "Standalone"; // Standalone, StudentSession, TutorSession
    public string Privacy { get; set; } = "Public"; // Public, ModuleSpecific, PrivateInviteOnly
    public Guid? ModuleId { get; set; }
    public int MaxParticipants { get; set; } = 10;
    public bool EnableRecording { get; set; } = false;
    public bool EnableChat { get; set; } = true;
}

public class CreateStudyRoomResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public StudyRoomDto? Room { get; set; }
}

public class JoinRoomRequest
{
    public Guid RoomId { get; set; }
    public string? RoomCode { get; set; } // For private rooms
}

public class JoinRoomResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public StudyRoomDto? Room { get; set; }
    public List<StudyRoomParticipantDto> CurrentParticipants { get; set; } = new();
}

public class StudyRoomParticipantDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? UserEmail { get; set; }
    public string? UserRole { get; set; }
    public string? ConnectionId { get; set; }
    public DateTime JoinedAt { get; set; }
    public bool IsActive { get; set; }
    public bool IsMuted { get; set; }
    public bool IsScreenSharing { get; set; }
    public bool IsVideoEnabled { get; set; } = true;
    public DateTime LastSeen { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsLarge { get; set; } = false; // For UI video size toggle
}

public class WebRTCSignalDto
{
    public string SignalType { get; set; } = string.Empty; // Offer, Answer, IceCandidate
    public Guid RoomId { get; set; }
    public Guid FromUserId { get; set; }
    public Guid ToUserId { get; set; }
    public string? Sdp { get; set; }
    public string? Offer { get; set; }
    public string? Answer { get; set; }
    public string? Candidate { get; set; }
    public string? SdpMid { get; set; }
    public int? SdpMLineIndex { get; set; }
}

public class RoomChatMessageDto
{
    public Guid MessageId { get; set; }
    public Guid RoomId { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }

    // TODO: File sharing support - implement backend later
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
    public long? FileSize { get; set; }
}

public class ParticipantStatusDto
{
    public Guid UserId { get; set; }
    public bool IsMuted { get; set; }
    public bool IsScreenSharing { get; set; }
    public bool IsVideoEnabled { get; set; }
}

public class StudyRoomFilters
{
    public string? RoomType { get; set; }
    public string? Privacy { get; set; }
    public Guid? ModuleId { get; set; }
    public string? Status { get; set; }
    public bool? OnlyMyRooms { get; set; }
}

