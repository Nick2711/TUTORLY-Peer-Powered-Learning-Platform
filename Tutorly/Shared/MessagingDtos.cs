namespace Tutorly.Shared
{
    // ============================================================================
    // CONVERSATION DTOs
    // ============================================================================

    public class ConversationDto
    {
        public int ConversationId { get; set; }
        public ConversationType ConversationType { get; set; }

        // Group-specific properties
        public string? GroupName { get; set; }
        public string? GroupDescription { get; set; }
        public string? GroupAvatarUrl { get; set; }
        public int ParticipantCount { get; set; }
        public int MaxParticipants { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? LastMessageAt { get; set; }
        public bool IsActive { get; set; }

        public List<ParticipantDto> Participants { get; set; } = new();
        public MessageDto? LastMessage { get; set; }
        public int UnreadCount { get; set; }

        // Current user's role in this conversation
        public ConversationRole CurrentUserRole { get; set; }
        public bool IsMuted { get; set; }
    }

    public class CreateConversationDto
    {
        public ConversationType Type { get; set; }

        // For direct messages (Type = Direct)
        public string? OtherUserId { get; set; }

        // For groups (Type = Group)
        public string? GroupName { get; set; }
        public string? GroupDescription { get; set; }
        public List<string>? ParticipantUserIds { get; set; }

        // Optional initial message
        public string? InitialMessage { get; set; }
    }

    public class UpdateGroupDto
    {
        public string? GroupName { get; set; }
        public string? GroupDescription { get; set; }
        public string? GroupAvatarUrl { get; set; }
    }

    public class ConversationSearchDto
    {
        public string SearchQuery { get; set; } = string.Empty;
        public ConversationType? Type { get; set; }
        public bool? IsUnreadOnly { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    // ============================================================================
    // PARTICIPANT DTOs
    // ============================================================================

    public class ParticipantDto
    {
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string Role { get; set; } = "student"; // student, tutor, admin

        // Role-specific IDs for search functionality
        public int? StudentId { get; set; }
        public int? TutorId { get; set; }
        public int? AdminId { get; set; }

        public ConversationRole ConversationRole { get; set; }
        public string? Nickname { get; set; }
        public DateTime JoinedAt { get; set; }
        public DateTime? LastReadAt { get; set; }
        public bool IsMuted { get; set; }
        public bool IsOnline { get; set; }
        public DateTime? LastSeenAt { get; set; }

        // Permissions
        public bool CanAddMembers { get; set; }
        public bool CanRemoveMembers { get; set; }
    }

    public class AddParticipantsDto
    {
        public List<string> UserIds { get; set; } = new();
        public ConversationRole Role { get; set; } = ConversationRole.Member;
    }

    public class UpdateParticipantRoleDto
    {
        public ConversationRole NewRole { get; set; }
        public bool? CanAddMembers { get; set; }
        public bool? CanRemoveMembers { get; set; }
    }

    // ============================================================================
    // MESSAGE DTOs
    // ============================================================================

    public class MessageDto
    {
        public int MessageId { get; set; }
        public int ConversationId { get; set; }
        public string SenderId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string SenderRole { get; set; } = "student";
        public string? SenderAvatar { get; set; }

        public string Content { get; set; } = string.Empty;
        public MessageType MessageType { get; set; }

        // File attachments
        public string? FileUrl { get; set; }
        public string? FileName { get; set; }
        public long? FileSize { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsEdited { get; set; }
        public bool IsDeleted { get; set; }

        // Reply/Thread
        public int? ReplyToMessageId { get; set; }
        public MessageDto? ReplyToMessage { get; set; }

        // Pinned messages (for groups)
        public bool IsPinned { get; set; }
        public DateTime? PinnedAt { get; set; }
        public string? PinnedByUserId { get; set; }

        // Read receipts
        public List<ReadReceiptDto> ReadBy { get; set; } = new();
        public int ReadCount { get; set; }
    }

    public class SendMessageDto
    {
        public string Content { get; set; } = string.Empty;
        public MessageType MessageType { get; set; } = MessageType.Text;
        public string? FileUrl { get; set; }
        public string? FileName { get; set; }
        public long? FileSize { get; set; }
        public int? ReplyToMessageId { get; set; }
    }

    public class EditMessageDto
    {
        public string Content { get; set; } = string.Empty;
    }

    public class MessageFilterDto
    {
        public int? BeforeMessageId { get; set; }
        public int? AfterMessageId { get; set; }
        public int Limit { get; set; } = 50;
    }

    public class ReadReceiptDto
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public DateTime ReadAt { get; set; }
    }

    public class MarkMessagesAsReadDto
    {
        public List<int> MessageIds { get; set; } = new();
    }

    // ============================================================================
    // INVITATION DTOs
    // ============================================================================

    public class GroupInvitationDto
    {
        public int InvitationId { get; set; }
        public int ConversationId { get; set; }
        public string ConversationName { get; set; } = string.Empty;
        public string InvitedByUserId { get; set; } = string.Empty;
        public string InvitedByName { get; set; } = string.Empty;
        public InvitationStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class InviteUsersDto
    {
        public List<string> UserIds { get; set; } = new();
    }

    // ============================================================================
    // USER SEARCH DTOs
    // ============================================================================

    public class UserSearchDto
    {
        public string SearchQuery { get; set; } = string.Empty;
        public string? RoleFilter { get; set; } // "student", "tutor", "admin", null = all
        public int PageSize { get; set; } = 20;
    }

    public class UserSearchResultDto
    {
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string? Programme { get; set; }
        public string? StudentId { get; set; } // For displaying student numbers
        public bool IsOnline { get; set; }
    }

    // ============================================================================
    // PRESENCE DTOs
    // ============================================================================

    public class PresenceDto
    {
        public string UserId { get; set; } = string.Empty;
        public PresenceStatus Status { get; set; }
        public DateTime LastSeenAt { get; set; }
    }

    public class UpdatePresenceDto
    {
        public PresenceStatus Status { get; set; }
    }

    // ============================================================================
    // TYPING INDICATOR DTOs
    // ============================================================================

    public class TypingIndicatorDto
    {
        public int ConversationId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string UserRole { get; set; } = string.Empty;
        public bool IsTyping { get; set; }
    }

    // ============================================================================
    // STATISTICS DTOs
    // ============================================================================

    public class UnreadCountDto
    {
        public int? ConversationId { get; set; }
        public int UnreadCount { get; set; }
    }

    public class AllUnreadCountsDto
    {
        public int TotalUnreadCount { get; set; }
        public List<UnreadCountDto> ConversationUnreadCounts { get; set; } = new();
    }
    public class EscalationRequestDto
    {
        public string Query { get; set; } = string.Empty;
        public int? ModuleId { get; set; }
        public string? ModuleName { get; set; }
        public EscalationType EscalationType { get; set; }
        public string? AdditionalContext { get; set; }
    }

    public class EscalationResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public ConversationDto? Conversation { get; set; }
        public List<TutorDto>? AvailableTutors { get; set; }
        public EscalationType EscalationType { get; set; }
    }

    public class TutorDto
    {
        public int TutorId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Programme { get; set; } = string.Empty;
        public string YearOfStudy { get; set; } = string.Empty;
        public double Rating { get; set; }
        public string? AvatarUrl { get; set; }
        public string Blurb { get; set; } = string.Empty;
        public bool IsOnline { get; set; }
    }

    public class EscalationNotificationDto
    {
        public int NotificationId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = "escalation"; // escalation, response, reminder
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; }
        public int? ConversationId { get; set; }
        public string? TutorName { get; set; }
    }
}
