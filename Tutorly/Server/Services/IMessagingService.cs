using Tutorly.Shared;

namespace Tutorly.Server.Services
{
    public interface IMessagingService
    {
        // ============================================================================
        // CONVERSATION OPERATIONS
        // ============================================================================

        /// <summary>
        /// Create a direct conversation between two users
        /// </summary>
        Task<ApiResponse<ConversationDto>> CreateDirectConversationAsync(string otherUserId, string currentUserId, string? initialMessage = null);

        /// <summary>
        /// Create a group conversation
        /// </summary>
        Task<ApiResponse<ConversationDto>> CreateGroupConversationAsync(CreateConversationDto dto, string currentUserId);

        /// <summary>
        /// Get all conversations for a user with filtering
        /// </summary>
        Task<ApiResponse<List<ConversationDto>>> GetConversationsAsync(ConversationSearchDto filter, string currentUserId);

        /// <summary>
        /// Get a specific conversation by ID
        /// </summary>
        Task<ApiResponse<ConversationDto>> GetConversationByIdAsync(int conversationId, string currentUserId);

        /// <summary>
        /// Update group details (name, description, avatar)
        /// </summary>
        Task<ApiResponse<ConversationDto>> UpdateGroupDetailsAsync(int conversationId, UpdateGroupDto dto, string currentUserId);

        /// <summary>
        /// Leave a conversation
        /// </summary>
        Task<ApiResponse<bool>> LeaveConversationAsync(int conversationId, string currentUserId);

        /// <summary>
        /// Delete a conversation (owner only for groups)
        /// </summary>
        Task<ApiResponse<bool>> DeleteConversationAsync(int conversationId, string currentUserId);

        /// <summary>
        /// Mute conversation notifications
        /// </summary>
        Task<ApiResponse<bool>> MuteConversationAsync(int conversationId, string currentUserId);

        /// <summary>
        /// Unmute conversation notifications
        /// </summary>
        Task<ApiResponse<bool>> UnmuteConversationAsync(int conversationId, string currentUserId);

        // ============================================================================
        // GROUP MEMBER MANAGEMENT
        // ============================================================================

        /// <summary>
        /// Add participants to a group conversation
        /// </summary>
        Task<ApiResponse<bool>> AddParticipantsAsync(int conversationId, AddParticipantsDto dto, string currentUserId);

        /// <summary>
        /// Remove a participant from a group
        /// </summary>
        Task<ApiResponse<bool>> RemoveParticipantAsync(int conversationId, string participantUserId, string currentUserId);

        /// <summary>
        /// Update a participant's role in a group
        /// </summary>
        Task<ApiResponse<bool>> UpdateParticipantRoleAsync(int conversationId, string participantUserId, UpdateParticipantRoleDto dto, string currentUserId);

        /// <summary>
        /// Update user's nickname in a group
        /// </summary>
        Task<ApiResponse<bool>> UpdateNicknameAsync(int conversationId, string nickname, string currentUserId);

        /// <summary>
        /// Get all participants in a conversation
        /// </summary>
        Task<ApiResponse<List<ParticipantDto>>> GetParticipantsAsync(int conversationId, string currentUserId);

        /// <summary>
        /// Invite users to a group
        /// </summary>
        Task<ApiResponse<bool>> InviteToGroupAsync(int conversationId, InviteUsersDto dto, string currentUserId);

        /// <summary>
        /// Respond to a group invitation
        /// </summary>
        Task<ApiResponse<bool>> RespondToInvitationAsync(int invitationId, bool accept, string currentUserId);

        /// <summary>
        /// Get pending invitations for current user
        /// </summary>
        Task<ApiResponse<List<GroupInvitationDto>>> GetPendingInvitationsAsync(string currentUserId);

        // ============================================================================
        // MESSAGE OPERATIONS
        // ============================================================================

        /// <summary>
        /// Send a message in a conversation
        /// </summary>
        Task<ApiResponse<MessageDto>> SendMessageAsync(int conversationId, SendMessageDto dto, string currentUserId);

        /// <summary>
        /// Get messages in a conversation with pagination
        /// </summary>
        Task<ApiResponse<List<MessageDto>>> GetMessagesAsync(int conversationId, MessageFilterDto filter, string currentUserId);

        /// <summary>
        /// Get a specific message by ID
        /// </summary>
        Task<ApiResponse<MessageDto>> GetMessageByIdAsync(int messageId, string currentUserId);

        /// <summary>
        /// Edit a message
        /// </summary>
        Task<ApiResponse<MessageDto>> EditMessageAsync(int messageId, EditMessageDto dto, string currentUserId);

        /// <summary>
        /// Delete a message (soft delete)
        /// </summary>
        Task<ApiResponse<bool>> DeleteMessageAsync(int messageId, string currentUserId);

        /// <summary>
        /// Pin a message (admin/owner only)
        /// </summary>
        Task<ApiResponse<bool>> PinMessageAsync(int messageId, string currentUserId);

        /// <summary>
        /// Unpin a message
        /// </summary>
        Task<ApiResponse<bool>> UnpinMessageAsync(int messageId, string currentUserId);

        /// <summary>
        /// Get pinned messages in a conversation
        /// </summary>
        Task<ApiResponse<List<MessageDto>>> GetPinnedMessagesAsync(int conversationId, string currentUserId);

        /// <summary>
        /// Mark messages as read
        /// </summary>
        Task<ApiResponse<bool>> MarkMessagesAsReadAsync(int conversationId, MarkMessagesAsReadDto dto, string currentUserId);

        /// <summary>
        /// Search messages in a conversation
        /// </summary>
        Task<ApiResponse<List<MessageDto>>> SearchMessagesAsync(int conversationId, string searchQuery, string currentUserId);

        // ============================================================================
        // USER SEARCH
        // ============================================================================

        /// <summary>
        /// Search for users to message
        /// </summary>
        Task<ApiResponse<List<UserSearchResultDto>>> SearchUsersAsync(UserSearchDto dto, string currentUserId);

        // ============================================================================
        // PRESENCE OPERATIONS
        // ============================================================================

        /// <summary>
        /// Update user's presence status
        /// </summary>
        Task<ApiResponse<bool>> UpdatePresenceAsync(string userId, PresenceStatus status);

        /// <summary>
        /// Get a user's presence
        /// </summary>
        Task<ApiResponse<PresenceDto>> GetUserPresenceAsync(string userId);

        /// <summary>
        /// Get presence for multiple users
        /// </summary>
        Task<ApiResponse<List<PresenceDto>>> GetMultiplePresencesAsync(List<string> userIds);

        // ============================================================================
        // TYPING INDICATORS
        // ============================================================================

        /// <summary>
        /// Start typing in a conversation
        /// </summary>
        Task<ApiResponse<bool>> StartTypingAsync(int conversationId, string userId);

        /// <summary>
        /// Stop typing in a conversation
        /// </summary>
        Task<ApiResponse<bool>> StopTypingAsync(int conversationId, string userId);

        // ============================================================================
        // STATISTICS
        // ============================================================================

        /// <summary>
        /// Get total unread message count for user
        /// </summary>
        Task<ApiResponse<int>> GetTotalUnreadCountAsync(string userId);

        /// <summary>
        /// Get unread count for a specific conversation
        /// </summary>
        Task<ApiResponse<int>> GetConversationUnreadCountAsync(int conversationId, string userId);

        /// <summary>
        /// Get unread counts for all user's conversations
        /// </summary>
        Task<ApiResponse<AllUnreadCountsDto>> GetUnreadCountsForAllConversationsAsync(string userId);
    }
}
