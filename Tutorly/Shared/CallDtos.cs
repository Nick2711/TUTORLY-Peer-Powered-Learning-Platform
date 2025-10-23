namespace Tutorly.Shared;

public class CallInvitationDto
{
    public Guid CallId { get; set; }
    public Guid RoomId { get; set; }
    public Guid FromUserId { get; set; }
    public string FromUserName { get; set; } = string.Empty;
    public Guid ToUserId { get; set; }
    public CallType CallType { get; set; }
    public DateTime CreatedAt { get; set; }
}

public enum CallType
{
    Audio,
    Video
}

public class CallResponseDto
{
    public Guid CallId { get; set; }
    public Guid RoomId { get; set; }
    public bool Accepted { get; set; }
    public Guid RespondingUserId { get; set; }
}

public class CallStateDto
{
    public Guid RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public CallType CallType { get; set; }
    public List<Guid> ParticipantIds { get; set; } = new();
    public bool IsMinimized { get; set; }
}
