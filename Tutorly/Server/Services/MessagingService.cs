using Supabase;
using Tutorly.Server.DatabaseModels;
using Tutorly.Server.Helpers;
using Tutorly.Shared;

namespace Tutorly.Server.Services
{
    public class MessagingService : IMessagingService
    {
        private readonly ISupabaseClientFactory _supabaseFactory;
        private readonly IContentFilterService _contentFilter;
        private readonly ILogger<MessagingService> _logger;

        public MessagingService(
            ISupabaseClientFactory supabaseFactory,
            IContentFilterService contentFilter,
            ILogger<MessagingService> logger)
        {
            _supabaseFactory = supabaseFactory;
            _contentFilter = contentFilter;
            _logger = logger;
        }

        #region Conversation Operations

        public async Task<ApiResponse<ConversationDto>> CreateDirectConversationAsync(string otherUserId, string currentUserId, string? initialMessage = null)
        {
            try
            {
                _logger.LogInformation($"Creating direct conversation between {currentUserId} and {otherUserId}");

                var client = _supabaseFactory.CreateService();

                // Check if conversation already exists
                var existingConversation = await GetOrCreateDirectConversationAsync(currentUserId, otherUserId, client);
                if (existingConversation != null)
                {
                    _logger.LogInformation($"Direct conversation already exists: {existingConversation.ConversationId}");

                    // Send initial message if provided
                    if (!string.IsNullOrWhiteSpace(initialMessage))
                    {
                        await SendMessageAsync(existingConversation.ConversationId, new SendMessageDto
                        {
                            Content = initialMessage,
                            MessageType = MessageType.Text
                        }, currentUserId);
                    }

                    return new ApiResponse<ConversationDto> { Success = true, Data = existingConversation };
                }

                // Create new conversation
                var conversation = new ConversationEntity
                {
                    ConversationType = "direct",
                    CreatedByUserId = currentUserId,
                    MaxParticipants = 2,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                var conversationResponse = await client
                    .From<ConversationEntity>()
                    .Insert(conversation);

                var createdConversation = conversationResponse.Models.First();

                // Add both participants
                var participants = new[]
                {
                    new ConversationParticipantEntity
                    {
                        ConversationId = createdConversation.ConversationId,
                        UserId = currentUserId,
                        Role = "member",
                        JoinedAt = DateTime.UtcNow
                    },
                    new ConversationParticipantEntity
                    {
                        ConversationId = createdConversation.ConversationId,
                        UserId = otherUserId,
                        Role = "member",
                        JoinedAt = DateTime.UtcNow
                    }
                };

                await client.From<ConversationParticipantEntity>().Insert(participants);

                // Send initial message if provided
                if (!string.IsNullOrWhiteSpace(initialMessage))
                {
                    await SendMessageAsync(createdConversation.ConversationId, new SendMessageDto
                    {
                        Content = initialMessage,
                        MessageType = MessageType.Text
                    }, currentUserId);
                }

                var result = await GetConversationByIdAsync(createdConversation.ConversationId, currentUserId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating direct conversation");
                return new ApiResponse<ConversationDto> { Success = false, Message = "Failed to create conversation" };
            }
        }

        public async Task<ApiResponse<ConversationDto>> CreateGroupConversationAsync(CreateConversationDto dto, string currentUserId)
        {
            try
            {
                _logger.LogInformation($"Creating group conversation: {dto.GroupName}");

                if (string.IsNullOrWhiteSpace(dto.GroupName))
                {
                    return new ApiResponse<ConversationDto> { Success = false, Message = "Group name is required" };
                }

                // Check for profanity
                var profanityCheck = await _contentFilter.CheckContentAsync(dto.GroupName + " " + dto.GroupDescription);
                if (!profanityCheck.IsClean)
                {
                    return new ApiResponse<ConversationDto>
                    {
                        Success = false,
                        Message = "Group name or description contains inappropriate language",
                        Errors = profanityCheck.Warnings
                    };
                }

                var client = _supabaseFactory.CreateService();

                // Create conversation
                var conversation = new ConversationEntity
                {
                    ConversationType = "group",
                    GroupName = dto.GroupName,
                    GroupDescription = dto.GroupDescription,
                    CreatedByUserId = currentUserId,
                    MaxParticipants = 50,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                var conversationResponse = await client
                    .From<ConversationEntity>()
                    .Insert(conversation);

                var createdConversation = conversationResponse.Models.First();

                // Add creator as owner
                var creatorParticipant = new ConversationParticipantEntity
                {
                    ConversationId = createdConversation.ConversationId,
                    UserId = currentUserId,
                    Role = "owner",
                    CanAddMembers = true,
                    CanRemoveMembers = true,
                    JoinedAt = DateTime.UtcNow
                };

                await client.From<ConversationParticipantEntity>().Insert(creatorParticipant);

                // Add other participants if provided
                if (dto.ParticipantUserIds != null && dto.ParticipantUserIds.Any())
                {
                    var otherParticipants = dto.ParticipantUserIds
                        .Where(id => id != currentUserId)
                        .Select(userId => new ConversationParticipantEntity
                        {
                            ConversationId = createdConversation.ConversationId,
                            UserId = userId,
                            Role = "member",
                            JoinedAt = DateTime.UtcNow
                        }).ToList();

                    if (otherParticipants.Any())
                    {
                        await client.From<ConversationParticipantEntity>().Insert(otherParticipants);
                    }

                    // Send system message about group creation
                    var participantNames = await GetUserNamesAsync(dto.ParticipantUserIds, client);
                    var systemMessage = $"Group created by you. Members: {string.Join(", ", participantNames)}";
                    await SendSystemMessageAsync(createdConversation.ConversationId, systemMessage, client);
                }

                // Send initial message if provided
                if (!string.IsNullOrWhiteSpace(dto.InitialMessage))
                {
                    await SendMessageAsync(createdConversation.ConversationId, new SendMessageDto
                    {
                        Content = dto.InitialMessage,
                        MessageType = MessageType.Text
                    }, currentUserId);
                }

                var result = await GetConversationByIdAsync(createdConversation.ConversationId, currentUserId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating group conversation");
                return new ApiResponse<ConversationDto> { Success = false, Message = "Failed to create group" };
            }
        }

        public async Task<ApiResponse<List<ConversationDto>>> GetConversationsAsync(ConversationSearchDto filter, string currentUserId)
        {
            try
            {
                _logger.LogInformation($"[GetConversationsAsync] Starting for user: {currentUserId}");
                var client = _supabaseFactory.CreateService();

                // Get user's conversations
                var participantQuery = client
                    .From<ConversationParticipantEntity>()
                    .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, currentUserId)
                    .Filter("left_at", Supabase.Postgrest.Constants.Operator.Is, (DateTime?)null);

                var participantsResponse = await participantQuery.Get();
                var conversationIds = participantsResponse.Models.Select(p => p.ConversationId).ToList();

                _logger.LogInformation($"[GetConversationsAsync] Found {conversationIds.Count} conversation IDs for user");

                if (!conversationIds.Any())
                {
                    _logger.LogInformation($"[GetConversationsAsync] No conversations found for user");
                    return new ApiResponse<List<ConversationDto>> { Success = true, Data = new List<ConversationDto>() };
                }

                // Get conversations
                // Workaround: Fetch conversations individually to avoid Supabase In operator issues
                _logger.LogInformation($"[GetConversationsAsync] Querying conversations with IDs: ({string.Join(",", conversationIds)})");

                var conversations = new List<ConversationEntity>();
                foreach (var convId in conversationIds)
                {
                    try
                    {
                        var conv = await client
                            .From<ConversationEntity>()
                            .Where(x => x.ConversationId == convId)
                            .Where(x => x.IsActive == true)
                            .Single();

                        if (conv != null)
                        {
                            conversations.Add(conv);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"[GetConversationsAsync] Failed to fetch conversation {convId}: {ex.Message}");
                    }
                }

                _logger.LogInformation($"[GetConversationsAsync] Retrieved {conversations.Count} conversation entities");

                // Apply filters
                if (filter.Type.HasValue)
                {
                    var typeStr = filter.Type.Value == ConversationType.Direct ? "direct" : "group";
                    conversations = conversations.Where(c => c.ConversationType == typeStr).ToList();
                }

                if (!string.IsNullOrWhiteSpace(filter.SearchQuery))
                {
                    conversations = conversations.Where(c =>
                        (c.GroupName?.Contains(filter.SearchQuery, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (c.GroupDescription?.Contains(filter.SearchQuery, StringComparison.OrdinalIgnoreCase) ?? false)
                    ).ToList();
                }

                // Build DTOs
                var result = new List<ConversationDto>();
                foreach (var conversation in conversations.OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt))
                {
                    var dto = await BuildConversationDtoAsync(conversation, currentUserId, client);
                    if (dto != null)
                    {
                        // Apply unread filter
                        if (filter.IsUnreadOnly == true && dto.UnreadCount == 0)
                            continue;

                        result.Add(dto);
                        _logger.LogInformation($"[GetConversationsAsync] Added conversation {dto.ConversationId} to result");
                    }
                    else
                    {
                        _logger.LogWarning($"[GetConversationsAsync] BuildConversationDtoAsync returned null for conversation {conversation.ConversationId}");
                    }
                }

                _logger.LogInformation($"[GetConversationsAsync] Returning {result.Count} conversations");
                return new ApiResponse<List<ConversationDto>> { Success = true, Data = result };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting conversations");
                return new ApiResponse<List<ConversationDto>> { Success = false, Message = "Failed to get conversations" };
            }
        }

        public async Task<ApiResponse<ConversationDto>> GetConversationByIdAsync(int conversationId, string currentUserId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                // Verify user is participant
                if (!await IsParticipantAsync(conversationId, currentUserId, client))
                {
                    return new ApiResponse<ConversationDto> { Success = false, Message = "Access denied" };
                }

                var conversationResponse = await client
                    .From<ConversationEntity>()
                    .Where(x => x.ConversationId == conversationId)
                    .Single();

                if (conversationResponse == null)
                {
                    return new ApiResponse<ConversationDto> { Success = false, Message = "Conversation not found" };
                }

                var dto = await BuildConversationDtoAsync(conversationResponse, currentUserId, client);
                return new ApiResponse<ConversationDto> { Success = true, Data = dto };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting conversation");
                return new ApiResponse<ConversationDto> { Success = false, Message = "Failed to get conversation" };
            }
        }

        public async Task<ApiResponse<ConversationDto>> UpdateGroupDetailsAsync(int conversationId, UpdateGroupDto dto, string currentUserId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                // Verify user is owner or admin
                var role = await GetUserRoleInConversationAsync(conversationId, currentUserId, client);
                if (role != "owner" && role != "admin")
                {
                    return new ApiResponse<ConversationDto> { Success = false, Message = "Only owners and admins can update group details" };
                }

                // Check for profanity if name or description changed
                if (!string.IsNullOrEmpty(dto.GroupName) || !string.IsNullOrEmpty(dto.GroupDescription))
                {
                    var profanityCheck = await _contentFilter.CheckContentAsync((dto.GroupName ?? "") + " " + (dto.GroupDescription ?? ""));
                    if (!profanityCheck.IsClean)
                    {
                        return new ApiResponse<ConversationDto>
                        {
                            Success = false,
                            Message = "Content contains inappropriate language",
                            Errors = profanityCheck.Warnings
                        };
                    }
                }

                var conversation = await client
                    .From<ConversationEntity>()
                    .Where(x => x.ConversationId == conversationId)
                    .Single();

                if (conversation == null)
                {
                    return new ApiResponse<ConversationDto> { Success = false, Message = "Conversation not found" };
                }

                if (!string.IsNullOrEmpty(dto.GroupName))
                    conversation.GroupName = dto.GroupName;
                if (!string.IsNullOrEmpty(dto.GroupDescription))
                    conversation.GroupDescription = dto.GroupDescription;
                if (!string.IsNullOrEmpty(dto.GroupAvatarUrl))
                    conversation.GroupAvatarUrl = dto.GroupAvatarUrl;

                conversation.UpdatedAt = DateTime.UtcNow;

                await client.From<ConversationEntity>().Update(conversation);

                return await GetConversationByIdAsync(conversationId, currentUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating group details");
                return new ApiResponse<ConversationDto> { Success = false, Message = "Failed to update group" };
            }
        }

        public async Task<ApiResponse<bool>> LeaveConversationAsync(int conversationId, string currentUserId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var participantQuery = client
                    .From<ConversationParticipantEntity>()
                    .Filter("conversation_id", Supabase.Postgrest.Constants.Operator.Equals, conversationId)
                    .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, currentUserId)
                    .Filter("left_at", Supabase.Postgrest.Constants.Operator.Is, (DateTime?)null);
                var participant = await participantQuery.Single();

                if (participant == null)
                {
                    return new ApiResponse<bool> { Success = false, Message = "Not a participant" };
                }

                participant.LeftAt = DateTime.UtcNow;
                await client.From<ConversationParticipantEntity>().Update(participant);

                // Send system message
                var profile = await GetUserProfileAsync(currentUserId, client);
                await SendSystemMessageAsync(conversationId, $"{profile?.FullName ?? "User"} left the conversation", client);

                _logger.LogInformation($"User {currentUserId} left conversation {conversationId}");
                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leaving conversation");
                return new ApiResponse<bool> { Success = false, Message = "Failed to leave conversation" };
            }
        }

        public async Task<ApiResponse<bool>> DeleteConversationAsync(int conversationId, string currentUserId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var role = await GetUserRoleInConversationAsync(conversationId, currentUserId, client);
                if (role != "owner")
                {
                    return new ApiResponse<bool> { Success = false, Message = "Only owners can delete conversations" };
                }

                var conversation = await client
                    .From<ConversationEntity>()
                    .Where(x => x.ConversationId == conversationId)
                    .Single();

                if (conversation == null)
                {
                    return new ApiResponse<bool> { Success = false, Message = "Conversation not found" };
                }

                conversation.IsActive = false;
                conversation.UpdatedAt = DateTime.UtcNow;

                await client.From<ConversationEntity>().Update(conversation);

                _logger.LogInformation($"Conversation {conversationId} deleted by {currentUserId}");
                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting conversation");
                return new ApiResponse<bool> { Success = false, Message = "Failed to delete conversation" };
            }
        }

        public async Task<ApiResponse<bool>> MuteConversationAsync(int conversationId, string currentUserId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var participantQuery = client
                    .From<ConversationParticipantEntity>()
                    .Filter("conversation_id", Supabase.Postgrest.Constants.Operator.Equals, conversationId)
                    .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, currentUserId)
                    .Filter("left_at", Supabase.Postgrest.Constants.Operator.Is, (DateTime?)null);
                var participant = await participantQuery.Single();

                if (participant == null)
                {
                    return new ApiResponse<bool> { Success = false, Message = "Not a participant" };
                }

                participant.IsMuted = true;
                await client.From<ConversationParticipantEntity>().Update(participant);

                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error muting conversation");
                return new ApiResponse<bool> { Success = false, Message = "Failed to mute conversation" };
            }
        }

        public async Task<ApiResponse<bool>> UnmuteConversationAsync(int conversationId, string currentUserId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var participantQuery = client
                    .From<ConversationParticipantEntity>()
                    .Filter("conversation_id", Supabase.Postgrest.Constants.Operator.Equals, conversationId)
                    .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, currentUserId)
                    .Filter("left_at", Supabase.Postgrest.Constants.Operator.Is, (DateTime?)null);
                var participant = await participantQuery.Single();

                if (participant == null)
                {
                    return new ApiResponse<bool> { Success = false, Message = "Not a participant" };
                }

                participant.IsMuted = false;
                await client.From<ConversationParticipantEntity>().Update(participant);

                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unmuting conversation");
                return new ApiResponse<bool> { Success = false, Message = "Failed to unmute conversation" };
            }
        }

        #endregion

        #region Group Member Management

        public async Task<ApiResponse<bool>> AddParticipantsAsync(int conversationId, AddParticipantsDto dto, string currentUserId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                // Verify user has permission
                var currentParticipantQuery = client
                    .From<ConversationParticipantEntity>()
                    .Filter("conversation_id", Supabase.Postgrest.Constants.Operator.Equals, conversationId)
                    .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, currentUserId)
                    .Filter("left_at", Supabase.Postgrest.Constants.Operator.Is, (DateTime?)null);
                var currentParticipant = await currentParticipantQuery.Single();

                if (currentParticipant == null || !currentParticipant.CanAddMembers)
                {
                    return new ApiResponse<bool> { Success = false, Message = "No permission to add members" };
                }

                // Add new participants
                var newParticipants = dto.UserIds.Select(userId => new ConversationParticipantEntity
                {
                    ConversationId = conversationId,
                    UserId = userId,
                    Role = dto.Role.ToString().ToLower(),
                    JoinedAt = DateTime.UtcNow
                }).ToList();

                await client.From<ConversationParticipantEntity>().Insert(newParticipants);

                // Send system message
                var names = await GetUserNamesAsync(dto.UserIds, client);
                await SendSystemMessageAsync(conversationId, $"Added {string.Join(", ", names)} to the group", client);

                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding participants");
                return new ApiResponse<bool> { Success = false, Message = "Failed to add participants" };
            }
        }

        public async Task<ApiResponse<bool>> RemoveParticipantAsync(int conversationId, string participantUserId, string currentUserId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                // Verify user has permission
                var currentParticipantQuery = client
                    .From<ConversationParticipantEntity>()
                    .Filter("conversation_id", Supabase.Postgrest.Constants.Operator.Equals, conversationId)
                    .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, currentUserId)
                    .Filter("left_at", Supabase.Postgrest.Constants.Operator.Is, (DateTime?)null);
                var currentParticipant = await currentParticipantQuery.Single();

                if (currentParticipant == null || !currentParticipant.CanRemoveMembers)
                {
                    return new ApiResponse<bool> { Success = false, Message = "No permission to remove members" };
                }

                var participantToRemoveQuery = client
                    .From<ConversationParticipantEntity>()
                    .Filter("conversation_id", Supabase.Postgrest.Constants.Operator.Equals, conversationId)
                    .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, participantUserId)
                    .Filter("left_at", Supabase.Postgrest.Constants.Operator.Is, (DateTime?)null);
                var participantToRemove = await participantToRemoveQuery.Single();

                if (participantToRemove == null)
                {
                    return new ApiResponse<bool> { Success = false, Message = "Participant not found" };
                }

                participantToRemove.LeftAt = DateTime.UtcNow;
                await client.From<ConversationParticipantEntity>().Update(participantToRemove);

                // Send system message
                var profile = await GetUserProfileAsync(participantUserId, client);
                await SendSystemMessageAsync(conversationId, $"{profile?.FullName ?? "User"} was removed from the group", client);

                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing participant");
                return new ApiResponse<bool> { Success = false, Message = "Failed to remove participant" };
            }
        }

        public async Task<ApiResponse<bool>> UpdateParticipantRoleAsync(int conversationId, string participantUserId, UpdateParticipantRoleDto dto, string currentUserId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                // Only owner can change roles
                var role = await GetUserRoleInConversationAsync(conversationId, currentUserId, client);
                if (role != "owner")
                {
                    return new ApiResponse<bool> { Success = false, Message = "Only owners can change roles" };
                }

                var participantQuery = client
                    .From<ConversationParticipantEntity>()
                    .Filter("conversation_id", Supabase.Postgrest.Constants.Operator.Equals, conversationId)
                    .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, participantUserId)
                    .Filter("left_at", Supabase.Postgrest.Constants.Operator.Is, (DateTime?)null);
                var participant = await participantQuery.Single();

                if (participant == null)
                {
                    return new ApiResponse<bool> { Success = false, Message = "Participant not found" };
                }

                participant.Role = dto.NewRole.ToString().ToLower();
                if (dto.CanAddMembers.HasValue)
                    participant.CanAddMembers = dto.CanAddMembers.Value;
                if (dto.CanRemoveMembers.HasValue)
                    participant.CanRemoveMembers = dto.CanRemoveMembers.Value;

                await client.From<ConversationParticipantEntity>().Update(participant);

                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating participant role");
                return new ApiResponse<bool> { Success = false, Message = "Failed to update role" };
            }
        }

        public async Task<ApiResponse<bool>> UpdateNicknameAsync(int conversationId, string nickname, string currentUserId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var participantQuery = client
                    .From<ConversationParticipantEntity>()
                    .Filter("conversation_id", Supabase.Postgrest.Constants.Operator.Equals, conversationId)
                    .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, currentUserId)
                    .Filter("left_at", Supabase.Postgrest.Constants.Operator.Is, (DateTime?)null);
                var participant = await participantQuery.Single();

                if (participant == null)
                {
                    return new ApiResponse<bool> { Success = false, Message = "Not a participant" };
                }

                participant.Nickname = nickname;
                await client.From<ConversationParticipantEntity>().Update(participant);

                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating nickname");
                return new ApiResponse<bool> { Success = false, Message = "Failed to update nickname" };
            }
        }

        public async Task<ApiResponse<List<ParticipantDto>>> GetParticipantsAsync(int conversationId, string currentUserId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                if (!await IsParticipantAsync(conversationId, currentUserId, client))
                {
                    return new ApiResponse<List<ParticipantDto>> { Success = false, Message = "Access denied" };
                }

                var participantsQuery = client
                    .From<ConversationParticipantEntity>()
                    .Filter("conversation_id", Supabase.Postgrest.Constants.Operator.Equals, conversationId)
                    .Filter("left_at", Supabase.Postgrest.Constants.Operator.Is, (DateTime?)null);
                var participantsResponse = await participantsQuery.Get();

                var participants = new List<ParticipantDto>();
                foreach (var participant in participantsResponse.Models)
                {
                    var profile = await GetUserProfileAsync(participant.UserId, client);
                    var presence = await GetUserPresenceInternalAsync(participant.UserId, client);

                    participants.Add(new ParticipantDto
                    {
                        UserId = participant.UserId,
                        FullName = profile?.FullName ?? "Unknown",
                        AvatarUrl = profile?.AvatarUrl,
                        Role = profile?.Role ?? "student",
                        ConversationRole = Enum.Parse<ConversationRole>(participant.Role, true),
                        Nickname = participant.Nickname,
                        JoinedAt = participant.JoinedAt,
                        LastReadAt = participant.LastReadAt,
                        IsMuted = participant.IsMuted,
                        IsOnline = presence?.Status == PresenceStatus.Online,
                        LastSeenAt = presence?.LastSeenAt,
                        CanAddMembers = participant.CanAddMembers,
                        CanRemoveMembers = participant.CanRemoveMembers
                    });
                }

                return new ApiResponse<List<ParticipantDto>> { Success = true, Data = participants };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting participants");
                return new ApiResponse<List<ParticipantDto>> { Success = false, Message = "Failed to get participants" };
            }
        }

        public async Task<ApiResponse<bool>> InviteToGroupAsync(int conversationId, InviteUsersDto dto, string currentUserId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                // Verify permission
                var participantQuery = client
                    .From<ConversationParticipantEntity>()
                    .Filter("conversation_id", Supabase.Postgrest.Constants.Operator.Equals, conversationId)
                    .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, currentUserId)
                    .Filter("left_at", Supabase.Postgrest.Constants.Operator.Is, (DateTime?)null);
                var participant = await participantQuery.Single();

                if (participant == null || !participant.CanAddMembers)
                {
                    return new ApiResponse<bool> { Success = false, Message = "No permission to invite" };
                }

                var invitations = dto.UserIds.Select(userId => new GroupInvitationEntity
                {
                    ConversationId = conversationId,
                    InvitedUserId = userId,
                    InvitedByUserId = currentUserId,
                    Status = "pending",
                    CreatedAt = DateTime.UtcNow
                }).ToList();

                await client.From<GroupInvitationEntity>().Insert(invitations);

                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending invitations");
                return new ApiResponse<bool> { Success = false, Message = "Failed to send invitations" };
            }
        }

        public async Task<ApiResponse<bool>> RespondToInvitationAsync(int invitationId, bool accept, string currentUserId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var invitation = await client
                    .From<GroupInvitationEntity>()
                    .Where(x => x.InvitationId == invitationId && x.InvitedUserId == currentUserId)
                    .Single();

                if (invitation == null)
                {
                    return new ApiResponse<bool> { Success = false, Message = "Invitation not found" };
                }

                invitation.Status = accept ? "accepted" : "declined";
                invitation.RespondedAt = DateTime.UtcNow;
                await client.From<GroupInvitationEntity>().Update(invitation);

                if (accept)
                {
                    // Add to group
                    var participant = new ConversationParticipantEntity
                    {
                        ConversationId = invitation.ConversationId,
                        UserId = currentUserId,
                        Role = "member",
                        JoinedAt = DateTime.UtcNow
                    };
                    await client.From<ConversationParticipantEntity>().Insert(participant);

                    // Send system message
                    var profile = await GetUserProfileAsync(currentUserId, client);
                    await SendSystemMessageAsync(invitation.ConversationId, $"{profile?.FullName ?? "User"} joined the group", client);
                }

                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error responding to invitation");
                return new ApiResponse<bool> { Success = false, Message = "Failed to respond" };
            }
        }

        public async Task<ApiResponse<List<GroupInvitationDto>>> GetPendingInvitationsAsync(string currentUserId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var invitationsResponse = await client
                    .From<GroupInvitationEntity>()
                    .Where(x => x.InvitedUserId == currentUserId && x.Status == "pending")
                    .Get();

                var result = new List<GroupInvitationDto>();
                foreach (var invitation in invitationsResponse.Models)
                {
                    var conversation = await client
                        .From<ConversationEntity>()
                        .Where(x => x.ConversationId == invitation.ConversationId)
                        .Single();

                    var inviter = await GetUserProfileAsync(invitation.InvitedByUserId, client);

                    result.Add(new GroupInvitationDto
                    {
                        InvitationId = invitation.InvitationId,
                        ConversationId = invitation.ConversationId,
                        ConversationName = conversation?.GroupName ?? "Group Chat",
                        InvitedByUserId = invitation.InvitedByUserId,
                        InvitedByName = inviter?.FullName ?? "Unknown",
                        Status = Enum.Parse<InvitationStatus>(invitation.Status, true),
                        CreatedAt = invitation.CreatedAt
                    });
                }

                return new ApiResponse<List<GroupInvitationDto>> { Success = true, Data = result };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting invitations");
                return new ApiResponse<List<GroupInvitationDto>> { Success = false, Message = "Failed to get invitations" };
            }
        }

        #endregion

        #region Message Operations

        public async Task<ApiResponse<MessageDto>> SendMessageAsync(int conversationId, SendMessageDto dto, string currentUserId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                if (!await IsParticipantAsync(conversationId, currentUserId, client))
                {
                    return new ApiResponse<MessageDto> { Success = false, Message = "Not a participant" };
                }

                // Check for profanity
                if (dto.MessageType == MessageType.Text)
                {
                    var profanityCheck = await _contentFilter.CheckContentAsync(dto.Content);
                    if (!profanityCheck.IsClean)
                    {
                        return new ApiResponse<MessageDto>
                        {
                            Success = false,
                            Message = "Message contains inappropriate language",
                            Errors = profanityCheck.Warnings
                        };
                    }
                }

                var message = new MessageEntity
                {
                    ConversationId = conversationId,
                    SenderUserId = currentUserId,
                    Content = dto.Content,
                    MessageType = dto.MessageType.ToString().ToLower(),
                    FileUrl = dto.FileUrl,
                    FileName = dto.FileName,
                    FileSize = dto.FileSize,
                    ReplyToMessageId = dto.ReplyToMessageId,
                    CreatedAt = DateTime.UtcNow
                };

                var messageResponse = await client
                    .From<MessageEntity>()
                    .Insert(message);

                var createdMessage = messageResponse.Models.First();

                return await GetMessageByIdAsync(createdMessage.MessageId, currentUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message");
                return new ApiResponse<MessageDto> { Success = false, Message = "Failed to send message" };
            }
        }

        public async Task<ApiResponse<List<MessageDto>>> GetMessagesAsync(int conversationId, MessageFilterDto filter, string currentUserId)
        {
            try
            {
                _logger.LogInformation($"[GetMessagesAsync] ✓ Started - ConversationId: {conversationId}, Limit: {filter.Limit}");

                var client = _supabaseFactory.CreateService();

                if (!await IsParticipantAsync(conversationId, currentUserId, client))
                {
                    _logger.LogWarning($"[GetMessagesAsync] ✗ User {currentUserId} not a participant in conversation {conversationId}");
                    return new ApiResponse<List<MessageDto>> { Success = false, Message = "Access denied" };
                }

                var query = client
                    .From<MessageEntity>()
                    .Where(x => x.ConversationId == conversationId)
                    .Filter("deleted_at", Supabase.Postgrest.Constants.Operator.Is, (DateTime?)null);

                if (filter.BeforeMessageId.HasValue)
                {
                    query = query.Filter("message_id", Supabase.Postgrest.Constants.Operator.LessThan, filter.BeforeMessageId.Value);
                }

                if (filter.AfterMessageId.HasValue)
                {
                    query = query.Filter("message_id", Supabase.Postgrest.Constants.Operator.GreaterThan, filter.AfterMessageId.Value);
                }

                var messagesResponse = await query
                    .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                    .Limit(filter.Limit)
                    .Get();

                _logger.LogInformation($"[GetMessagesAsync] ✓ Found {messagesResponse.Models?.Count ?? 0} messages in database");

                var result = new List<MessageDto>();
                if (messagesResponse.Models != null)
                {
                    foreach (var message in messagesResponse.Models)
                    {
                        var dto = await BuildMessageDtoAsync(message, currentUserId, client);
                        if (dto != null)
                        {
                            result.Add(dto);
                        }
                    }
                }

                _logger.LogInformation($"[GetMessagesAsync] ✓ Built {result.Count} MessageDTOs");

                result.Reverse(); // Return in chronological order
                return new ApiResponse<List<MessageDto>> { Success = true, Data = result };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[GetMessagesAsync] ✗ ERROR for conversation {conversationId}");
                return new ApiResponse<List<MessageDto>> { Success = false, Message = "Failed to get messages" };
            }
        }

        public async Task<ApiResponse<MessageDto>> GetMessageByIdAsync(int messageId, string currentUserId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var message = await client
                    .From<MessageEntity>()
                    .Where(x => x.MessageId == messageId)
                    .Single();

                if (message == null)
                {
                    return new ApiResponse<MessageDto> { Success = false, Message = "Message not found" };
                }

                if (!await IsParticipantAsync(message.ConversationId, currentUserId, client))
                {
                    return new ApiResponse<MessageDto> { Success = false, Message = "Access denied" };
                }

                var dto = await BuildMessageDtoAsync(message, currentUserId, client);
                return new ApiResponse<MessageDto> { Success = true, Data = dto };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting message");
                return new ApiResponse<MessageDto> { Success = false, Message = "Failed to get message" };
            }
        }

        public async Task<ApiResponse<MessageDto>> EditMessageAsync(int messageId, EditMessageDto dto, string currentUserId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var message = await client
                    .From<MessageEntity>()
                    .Where(x => x.MessageId == messageId)
                    .Single();

                if (message == null)
                {
                    return new ApiResponse<MessageDto> { Success = false, Message = "Message not found" };
                }

                if (message.SenderUserId != currentUserId)
                {
                    return new ApiResponse<MessageDto> { Success = false, Message = "Can only edit your own messages" };
                }

                // Check for profanity
                var profanityCheck = await _contentFilter.CheckContentAsync(dto.Content);
                if (!profanityCheck.IsClean)
                {
                    return new ApiResponse<MessageDto>
                    {
                        Success = false,
                        Message = "Content contains inappropriate language",
                        Errors = profanityCheck.Warnings
                    };
                }

                message.Content = dto.Content;
                message.IsEdited = true;
                message.UpdatedAt = DateTime.UtcNow;

                await client.From<MessageEntity>().Update(message);

                return await GetMessageByIdAsync(messageId, currentUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing message");
                return new ApiResponse<MessageDto> { Success = false, Message = "Failed to edit message" };
            }
        }

        public async Task<ApiResponse<bool>> DeleteMessageAsync(int messageId, string currentUserId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var message = await client
                    .From<MessageEntity>()
                    .Where(x => x.MessageId == messageId)
                    .Single();

                if (message == null)
                {
                    return new ApiResponse<bool> { Success = false, Message = "Message not found" };
                }

                // User can delete their own messages, or admins/owners can delete any message
                if (message.SenderUserId != currentUserId)
                {
                    var role = await GetUserRoleInConversationAsync(message.ConversationId, currentUserId, client);
                    if (role != "owner" && role != "admin")
                    {
                        return new ApiResponse<bool> { Success = false, Message = "No permission to delete" };
                    }
                }

                message.DeletedAt = DateTime.UtcNow;
                await client.From<MessageEntity>().Update(message);

                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message");
                return new ApiResponse<bool> { Success = false, Message = "Failed to delete message" };
            }
        }

        public async Task<ApiResponse<bool>> PinMessageAsync(int messageId, string currentUserId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var message = await client
                    .From<MessageEntity>()
                    .Where(x => x.MessageId == messageId)
                    .Single();

                if (message == null)
                {
                    return new ApiResponse<bool> { Success = false, Message = "Message not found" };
                }

                var role = await GetUserRoleInConversationAsync(message.ConversationId, currentUserId, client);
                if (role != "owner" && role != "admin")
                {
                    return new ApiResponse<bool> { Success = false, Message = "Only admins and owners can pin messages" };
                }

                message.IsPinned = true;
                message.PinnedAt = DateTime.UtcNow;
                message.PinnedByUserId = currentUserId;

                await client.From<MessageEntity>().Update(message);

                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pinning message");
                return new ApiResponse<bool> { Success = false, Message = "Failed to pin message" };
            }
        }

        public async Task<ApiResponse<bool>> UnpinMessageAsync(int messageId, string currentUserId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var message = await client
                    .From<MessageEntity>()
                    .Where(x => x.MessageId == messageId)
                    .Single();

                if (message == null)
                {
                    return new ApiResponse<bool> { Success = false, Message = "Message not found" };
                }

                var role = await GetUserRoleInConversationAsync(message.ConversationId, currentUserId, client);
                if (role != "owner" && role != "admin")
                {
                    return new ApiResponse<bool> { Success = false, Message = "Only admins and owners can unpin messages" };
                }

                message.IsPinned = false;
                message.PinnedAt = null;
                message.PinnedByUserId = null;

                await client.From<MessageEntity>().Update(message);

                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unpinning message");
                return new ApiResponse<bool> { Success = false, Message = "Failed to unpin message" };
            }
        }

        public async Task<ApiResponse<List<MessageDto>>> GetPinnedMessagesAsync(int conversationId, string currentUserId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                if (!await IsParticipantAsync(conversationId, currentUserId, client))
                {
                    return new ApiResponse<List<MessageDto>> { Success = false, Message = "Access denied" };
                }

                var messagesResponse = await client
                    .From<MessageEntity>()
                    .Where(x => x.ConversationId == conversationId && x.IsPinned == true)
                    .Filter("deleted_at", Supabase.Postgrest.Constants.Operator.Is, (DateTime?)null)
                    .Order("pinned_at", Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get();

                var result = new List<MessageDto>();
                foreach (var message in messagesResponse.Models)
                {
                    var dto = await BuildMessageDtoAsync(message, currentUserId, client);
                    if (dto != null)
                    {
                        result.Add(dto);
                    }
                }

                return new ApiResponse<List<MessageDto>> { Success = true, Data = result };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pinned messages");
                return new ApiResponse<List<MessageDto>> { Success = false, Message = "Failed to get pinned messages" };
            }
        }

        public async Task<ApiResponse<bool>> MarkMessagesAsReadAsync(int conversationId, MarkMessagesAsReadDto dto, string currentUserId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                if (!await IsParticipantAsync(conversationId, currentUserId, client))
                {
                    return new ApiResponse<bool> { Success = false, Message = "Access denied" };
                }

                var readReceipts = dto.MessageIds.Select(messageId => new MessageReadEntity
                {
                    MessageId = messageId,
                    UserId = currentUserId,
                    ReadAt = DateTime.UtcNow
                }).ToList();

                // Insert will fail silently if already exists (due to unique constraint)
                try
                {
                    await client.From<MessageReadEntity>().Insert(readReceipts);
                }
                catch
                {
                    // Ignore duplicate key errors
                }

                // Update last_read_at
                var participantQuery = client
                    .From<ConversationParticipantEntity>()
                    .Filter("conversation_id", Supabase.Postgrest.Constants.Operator.Equals, conversationId)
                    .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, currentUserId)
                    .Filter("left_at", Supabase.Postgrest.Constants.Operator.Is, (DateTime?)null);
                var participant = await participantQuery.Single();

                if (participant != null)
                {
                    participant.LastReadAt = DateTime.UtcNow;
                    await client.From<ConversationParticipantEntity>().Update(participant);
                }

                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as read");
                return new ApiResponse<bool> { Success = false, Message = "Failed to mark messages as read" };
            }
        }

        public async Task<ApiResponse<List<MessageDto>>> SearchMessagesAsync(int conversationId, string searchQuery, string currentUserId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                if (!await IsParticipantAsync(conversationId, currentUserId, client))
                {
                    return new ApiResponse<List<MessageDto>> { Success = false, Message = "Access denied" };
                }

                // Simple search - in production, use full-text search
                var messagesResponse = await client
                    .From<MessageEntity>()
                    .Where(x => x.ConversationId == conversationId)
                    .Filter("deleted_at", Supabase.Postgrest.Constants.Operator.Is, (DateTime?)null)
                    .Get();

                var filteredMessages = messagesResponse.Models
                    .Where(m => m.Content.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(m => m.CreatedAt)
                    .Take(50)
                    .ToList();

                var result = new List<MessageDto>();
                foreach (var message in filteredMessages)
                {
                    var dto = await BuildMessageDtoAsync(message, currentUserId, client);
                    if (dto != null)
                    {
                        result.Add(dto);
                    }
                }

                return new ApiResponse<List<MessageDto>> { Success = true, Data = result };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching messages");
                return new ApiResponse<List<MessageDto>> { Success = false, Message = "Failed to search messages" };
            }
        }

        #endregion

        #region User Search

        public async Task<ApiResponse<List<UserSearchResultDto>>> SearchUsersAsync(UserSearchDto dto, string currentUserId)
        {
            try
            {
                _logger.LogInformation($"[SearchUsersAsync] ========================================");
                _logger.LogInformation($"[SearchUsersAsync] Starting search - Query: '{dto.SearchQuery}', CurrentUser: {currentUserId}");

                var client = _supabaseFactory.CreateService();

                var result = new List<UserSearchResultDto>();

                // Search students
                if (string.IsNullOrEmpty(dto.RoleFilter) || dto.RoleFilter == "student")
                {
                    _logger.LogInformation("[SearchUsersAsync] --- SEARCHING STUDENTS ---");

                    var students = await client
                        .From<StudentProfileEntity>()
                        .Get();

                    _logger.LogInformation($"[SearchUsersAsync] Total students in DB: {students.Models?.Count ?? 0}");

                    var filteredStudents = students.Models?
                        .Where(s => s != null &&
                                   !string.IsNullOrEmpty(s.UserId) &&
                                   s.UserId != currentUserId &&
                                   (string.IsNullOrEmpty(dto.SearchQuery) ||
                                    (!string.IsNullOrEmpty(s.FullName) && s.FullName.Contains(dto.SearchQuery, StringComparison.OrdinalIgnoreCase)) ||
                                    s.StudentId.ToString().Contains(dto.SearchQuery, StringComparison.OrdinalIgnoreCase)))
                        .Take(dto.PageSize) ?? Enumerable.Empty<StudentProfileEntity>();

                    _logger.LogInformation($"[SearchUsersAsync] Filtered students (matching query): {filteredStudents.Count()}");

                    foreach (var student in filteredStudents)
                    {
                        _logger.LogInformation($"  ✓ Adding student: {student.FullName} (StudentId: {student.StudentId})");
                        var presence = await GetUserPresenceInternalAsync(student.UserId, client);
                        result.Add(new UserSearchResultDto
                        {
                            UserId = student.UserId,
                            FullName = string.IsNullOrEmpty(student.FullName) ? $"Student {student.StudentId}" : student.FullName,
                            Role = "student",
                            AvatarUrl = student.AvatarUrl,
                            Programme = student.Programme,
                            StudentId = student.StudentId.ToString(),
                            IsOnline = presence?.Status == PresenceStatus.Online
                        });
                    }
                }

                // Search tutors
                if (string.IsNullOrEmpty(dto.RoleFilter) || dto.RoleFilter == "tutor")
                {
                    _logger.LogInformation($"[SearchUsersAsync] --- SEARCHING TUTORS ---");

                    var tutors = await client
                        .From<TutorProfileEntity>()
                        .Get();

                    _logger.LogInformation($"[SearchUsersAsync] Total tutors in DB: {tutors.Models?.Count ?? 0}");

                    // Log first few tutors for debugging
                    if (tutors.Models != null && tutors.Models.Count > 0)
                    {
                        _logger.LogInformation("[SearchUsersAsync] Sample tutors from DB:");
                        foreach (var t in tutors.Models.Take(3))
                        {
                            _logger.LogInformation($"    UserId={t.UserId}, Name='{t.FullName ?? "NULL"}', TutorId={t.TutorId}");
                        }
                    }

                    var filteredTutors = tutors.Models?
                        .Where(t => t != null &&
                                   !string.IsNullOrEmpty(t.UserId) &&
                                   t.UserId != currentUserId &&
                                   (string.IsNullOrEmpty(dto.SearchQuery) ||
                                    (!string.IsNullOrEmpty(t.FullName) && t.FullName.Contains(dto.SearchQuery, StringComparison.OrdinalIgnoreCase)) ||
                                    t.TutorId.ToString().Contains(dto.SearchQuery, StringComparison.OrdinalIgnoreCase)))
                        .Take(dto.PageSize) ?? Enumerable.Empty<TutorProfileEntity>();

                    _logger.LogInformation($"[SearchUsersAsync] Filtered tutors (matching query '{dto.SearchQuery}'): {filteredTutors.Count()}");

                    foreach (var tutor in filteredTutors)
                    {
                        _logger.LogInformation($"  ✓ Adding tutor: {tutor.FullName ?? $"Tutor {tutor.TutorId}"} (TutorId: {tutor.TutorId})");
                        var presence = await GetUserPresenceInternalAsync(tutor.UserId, client);
                        result.Add(new UserSearchResultDto
                        {
                            UserId = tutor.UserId,
                            FullName = string.IsNullOrEmpty(tutor.FullName) ? $"Tutor {tutor.TutorId}" : tutor.FullName,
                            Role = "tutor",
                            AvatarUrl = tutor.AvatarUrl,
                            Programme = tutor.Programme,
                            StudentId = tutor.TutorId.ToString(), // Use TutorId as the display ID
                            IsOnline = presence?.Status == PresenceStatus.Online
                        });
                    }
                }

                // Search admins
                if (string.IsNullOrEmpty(dto.RoleFilter) || dto.RoleFilter == "admin")
                {
                    _logger.LogInformation("[SearchUsersAsync] --- SEARCHING ADMINS ---");

                    var admins = await client
                        .From<AdminProfileEntity>()
                        .Get();

                    _logger.LogInformation($"[SearchUsersAsync] Total admins in DB: {admins.Models?.Count ?? 0}");

                    var filteredAdmins = admins.Models?
                        .Where(a => a != null &&
                                   !string.IsNullOrEmpty(a.UserId) &&
                                   a.UserId != currentUserId &&
                                   (string.IsNullOrEmpty(dto.SearchQuery) ||
                                    (!string.IsNullOrEmpty(a.FullName) && a.FullName.Contains(dto.SearchQuery, StringComparison.OrdinalIgnoreCase)) ||
                                    a.AdminId.ToString().Contains(dto.SearchQuery, StringComparison.OrdinalIgnoreCase)))
                        .Take(dto.PageSize) ?? Enumerable.Empty<AdminProfileEntity>();

                    _logger.LogInformation($"[SearchUsersAsync] Filtered admins (matching query): {filteredAdmins.Count()}");

                    foreach (var admin in filteredAdmins)
                    {
                        _logger.LogInformation($"  ✓ Adding admin: {admin.FullName} (AdminId: {admin.AdminId})");
                        var presence = await GetUserPresenceInternalAsync(admin.UserId, client);
                        result.Add(new UserSearchResultDto
                        {
                            UserId = admin.UserId,
                            FullName = string.IsNullOrEmpty(admin.FullName) ? $"Admin {admin.AdminId}" : admin.FullName,
                            Role = "admin",
                            AvatarUrl = null, // Admins don't have avatars by default
                            Programme = null,
                            StudentId = admin.AdminId.ToString(), // Use AdminId as the display ID
                            IsOnline = presence?.Status == PresenceStatus.Online
                        });
                    }
                }

                _logger.LogInformation($"[SearchUsersAsync] ========================================");
                _logger.LogInformation($"[SearchUsersAsync] FINAL RESULTS: {result.Count} total users found");
                foreach (var user in result.Take(5))
                {
                    _logger.LogInformation($"  • {user.Role.ToUpper()}: {user.FullName} (ID: {user.StudentId})");
                }
                _logger.LogInformation($"[SearchUsersAsync] ========================================");

                return new ApiResponse<List<UserSearchResultDto>> { Success = true, Data = result.Take(dto.PageSize).ToList() };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching users");
                return new ApiResponse<List<UserSearchResultDto>> { Success = false, Message = "Failed to search users" };
            }
        }

        #endregion

        #region Presence Operations

        public async Task<ApiResponse<bool>> UpdatePresenceAsync(string userId, PresenceStatus status)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var presence = await client
                    .From<UserPresenceEntity>()
                    .Where(x => x.UserId == userId)
                    .Single();

                if (presence == null)
                {
                    // Create new presence
                    presence = new UserPresenceEntity
                    {
                        UserId = userId,
                        Status = status.ToString().ToLower(),
                        LastSeenAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    await client.From<UserPresenceEntity>().Insert(presence);
                }
                else
                {
                    presence.Status = status.ToString().ToLower();
                    presence.LastSeenAt = DateTime.UtcNow;
                    presence.UpdatedAt = DateTime.UtcNow;
                    await client.From<UserPresenceEntity>().Update(presence);
                }

                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating presence");
                return new ApiResponse<bool> { Success = false, Message = "Failed to update presence" };
            }
        }

        public async Task<ApiResponse<PresenceDto>> GetUserPresenceAsync(string userId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();
                var presence = await GetUserPresenceInternalAsync(userId, client);

                if (presence == null)
                {
                    return new ApiResponse<PresenceDto>
                    {
                        Success = true,
                        Data = new PresenceDto
                        {
                            UserId = userId,
                            Status = PresenceStatus.Offline,
                            LastSeenAt = DateTime.UtcNow
                        }
                    };
                }

                return new ApiResponse<PresenceDto> { Success = true, Data = presence };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting presence");
                return new ApiResponse<PresenceDto> { Success = false, Message = "Failed to get presence" };
            }
        }

        public async Task<ApiResponse<List<PresenceDto>>> GetMultiplePresencesAsync(List<string> userIds)
        {
            try
            {
                var client = _supabaseFactory.CreateService();
                var result = new List<PresenceDto>();

                foreach (var userId in userIds)
                {
                    var presence = await GetUserPresenceInternalAsync(userId, client);
                    if (presence != null)
                    {
                        result.Add(presence);
                    }
                }

                return new ApiResponse<List<PresenceDto>> { Success = true, Data = result };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting multiple presences");
                return new ApiResponse<List<PresenceDto>> { Success = false, Message = "Failed to get presences" };
            }
        }

        #endregion

        #region Typing Indicators

        public async Task<ApiResponse<bool>> StartTypingAsync(int conversationId, string userId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var typing = new TypingIndicatorEntity
                {
                    ConversationId = conversationId,
                    UserId = userId,
                    StartedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(10)
                };

                // Upsert (insert or update)
                try
                {
                    await client.From<TypingIndicatorEntity>().Insert(typing);
                }
                catch
                {
                    // If exists, update
                    var existing = await client
                        .From<TypingIndicatorEntity>()
                        .Where(x => x.ConversationId == conversationId && x.UserId == userId)
                        .Single();

                    if (existing != null)
                    {
                        existing.StartedAt = DateTime.UtcNow;
                        existing.ExpiresAt = DateTime.UtcNow.AddSeconds(10);
                        await client.From<TypingIndicatorEntity>().Update(existing);
                    }
                }

                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting typing");
                return new ApiResponse<bool> { Success = false, Message = "Failed to start typing" };
            }
        }

        public async Task<ApiResponse<bool>> StopTypingAsync(int conversationId, string userId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                await client
                    .From<TypingIndicatorEntity>()
                    .Where(x => x.ConversationId == conversationId && x.UserId == userId)
                    .Delete();

                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping typing");
                return new ApiResponse<bool> { Success = false, Message = "Failed to stop typing" };
            }
        }

        #endregion

        #region Statistics

        public async Task<ApiResponse<int>> GetTotalUnreadCountAsync(string userId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                // Get all user's conversations
                var participantsQuery = client
                    .From<ConversationParticipantEntity>()
                    .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, userId)
                    .Filter("left_at", Supabase.Postgrest.Constants.Operator.Is, (DateTime?)null);
                var participantsResponse = await participantsQuery.Get();

                int totalUnread = 0;
                foreach (var participant in participantsResponse.Models)
                {
                    var unread = await GetConversationUnreadCountInternalAsync(participant.ConversationId, userId, participant.LastReadAt, client);
                    totalUnread += unread;
                }

                return new ApiResponse<int> { Success = true, Data = totalUnread };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting total unread count");
                return new ApiResponse<int> { Success = false, Message = "Failed to get unread count" };
            }
        }

        public async Task<ApiResponse<int>> GetConversationUnreadCountAsync(int conversationId, string userId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var participantQuery = client
                    .From<ConversationParticipantEntity>()
                    .Filter("conversation_id", Supabase.Postgrest.Constants.Operator.Equals, conversationId)
                    .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, userId)
                    .Filter("left_at", Supabase.Postgrest.Constants.Operator.Is, (DateTime?)null);
                var participant = await participantQuery.Single();

                if (participant == null)
                {
                    return new ApiResponse<int> { Success = false, Message = "Not a participant" };
                }

                var count = await GetConversationUnreadCountInternalAsync(conversationId, userId, participant.LastReadAt, client);
                return new ApiResponse<int> { Success = true, Data = count };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting conversation unread count");
                return new ApiResponse<int> { Success = false, Message = "Failed to get unread count" };
            }
        }

        public async Task<ApiResponse<AllUnreadCountsDto>> GetUnreadCountsForAllConversationsAsync(string userId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var participantsQuery = client
                    .From<ConversationParticipantEntity>()
                    .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, userId)
                    .Filter("left_at", Supabase.Postgrest.Constants.Operator.Is, (DateTime?)null);
                var participantsResponse = await participantsQuery.Get();

                var result = new AllUnreadCountsDto();
                foreach (var participant in participantsResponse.Models)
                {
                    var unread = await GetConversationUnreadCountInternalAsync(participant.ConversationId, userId, participant.LastReadAt, client);
                    result.ConversationUnreadCounts.Add(new UnreadCountDto
                    {
                        ConversationId = participant.ConversationId,
                        UnreadCount = unread
                    });
                    result.TotalUnreadCount += unread;
                }

                return new ApiResponse<AllUnreadCountsDto> { Success = true, Data = result };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all unread counts");
                return new ApiResponse<AllUnreadCountsDto> { Success = false, Message = "Failed to get unread counts" };
            }
        }

        #endregion

        #region Helper Methods

        private async Task<ConversationDto?> GetOrCreateDirectConversationAsync(string userId1, string userId2, Supabase.Client client)
        {
            // Get all conversations where both users are participants
            var user1Query = client
                .From<ConversationParticipantEntity>()
                .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, userId1)
                .Filter("left_at", Supabase.Postgrest.Constants.Operator.Is, (DateTime?)null);
            var user1Conversations = await user1Query.Get();

            var user2Query = client
                .From<ConversationParticipantEntity>()
                .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, userId2)
                .Filter("left_at", Supabase.Postgrest.Constants.Operator.Is, (DateTime?)null);
            var user2Conversations = await user2Query.Get();

            var commonConversationIds = user1Conversations.Models
                .Select(p => p.ConversationId)
                .Intersect(user2Conversations.Models.Select(p => p.ConversationId))
                .ToList();

            // Find direct conversations
            foreach (var conversationId in commonConversationIds)
            {
                var conversation = await client
                    .From<ConversationEntity>()
                    .Where(x => x.ConversationId == conversationId && x.ConversationType == "direct")
                    .Single();

                if (conversation != null)
                {
                    return await BuildConversationDtoAsync(conversation, userId1, client);
                }
            }

            return null;
        }

        private async Task<bool> IsParticipantAsync(int conversationId, string userId, Supabase.Client client)
        {
            var participantQuery = client
                .From<ConversationParticipantEntity>()
                .Filter("conversation_id", Supabase.Postgrest.Constants.Operator.Equals, conversationId)
                .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, userId)
                .Filter("left_at", Supabase.Postgrest.Constants.Operator.Is, (DateTime?)null);
            var participant = await participantQuery.Single();

            return participant != null;
        }

        private async Task<string?> GetUserRoleInConversationAsync(int conversationId, string userId, Supabase.Client client)
        {
            var participantQuery = client
                .From<ConversationParticipantEntity>()
                .Filter("conversation_id", Supabase.Postgrest.Constants.Operator.Equals, conversationId)
                .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, userId)
                .Filter("left_at", Supabase.Postgrest.Constants.Operator.Is, (DateTime?)null);
            var participant = await participantQuery.Single();

            return participant?.Role;
        }

        private async Task<UnifiedProfileDto?> GetUserProfileAsync(string userId, Supabase.Client client)
        {
            // Try student first
            var studentProfile = await client
                .From<StudentProfileEntity>()
                .Where(x => x.UserId == userId)
                .Single();

            if (studentProfile != null)
            {
                return new UnifiedProfileDto
                {
                    Role = "student",
                    FullName = studentProfile.FullName,
                    AvatarUrl = studentProfile.AvatarUrl,
                    StudentId = studentProfile.StudentId,
                    Programme = studentProfile.Programme
                };
            }

            // Try tutor
            var tutorProfile = await client
                .From<TutorProfileEntity>()
                .Where(x => x.UserId == userId)
                .Single();

            if (tutorProfile != null)
            {
                return new UnifiedProfileDto
                {
                    Role = "tutor",
                    FullName = tutorProfile.FullName,
                    AvatarUrl = tutorProfile.AvatarUrl,
                    TutorId = tutorProfile.TutorId,
                    Programme = tutorProfile.Programme
                };
            }

            return null;
        }

        private async Task<List<string>> GetUserNamesAsync(List<string> userIds, Supabase.Client client)
        {
            var names = new List<string>();
            foreach (var userId in userIds)
            {
                var profile = await GetUserProfileAsync(userId, client);
                names.Add(profile?.FullName ?? "Unknown");
            }
            return names;
        }

        private async Task SendSystemMessageAsync(int conversationId, string content, Supabase.Client client)
        {
            // Get the first participant to use as the sender for system messages
            var participants = await client.From<ConversationParticipantEntity>()
                .Where(p => p.ConversationId == conversationId)
                .Filter("left_at", Supabase.Postgrest.Constants.Operator.Is, (DateTime?)null)
                .Get();

            if (participants.Models?.Any() == true)
            {
                var firstParticipant = participants.Models.First();
                var message = new MessageEntity
                {
                    ConversationId = conversationId,
                    SenderUserId = firstParticipant.UserId,
                    Content = content,
                    MessageType = "system",
                    CreatedAt = DateTime.UtcNow
                };

                await client.From<MessageEntity>().Insert(message);
            }
        }

        private async Task<ConversationDto?> BuildConversationDtoAsync(ConversationEntity conversation, string currentUserId, Supabase.Client client)
        {
            // Get participants
            var participantsQuery = client
                .From<ConversationParticipantEntity>()
                .Filter("conversation_id", Supabase.Postgrest.Constants.Operator.Equals, conversation.ConversationId)
                .Filter("left_at", Supabase.Postgrest.Constants.Operator.Is, (DateTime?)null);
            var participantsResponse = await participantsQuery.Get();

            var currentUserParticipant = participantsResponse.Models.FirstOrDefault(p => p.UserId == currentUserId);
            if (currentUserParticipant == null)
                return null;

            var participants = new List<ParticipantDto>();
            foreach (var participant in participantsResponse.Models)
            {
                var profile = await GetUserProfileAsync(participant.UserId, client);
                var presence = await GetUserPresenceInternalAsync(participant.UserId, client);

                // Determine display name with fallbacks for empty/null names
                var displayName = !string.IsNullOrWhiteSpace(profile?.FullName)
                    ? profile.FullName
                    : (profile?.Role == "student" ? $"Student {profile?.StudentId}"
                       : profile?.Role == "tutor" ? $"Tutor {profile?.TutorId}"
                       : profile?.Role == "admin" ? $"Admin {profile?.AdminId}"
                       : "Unknown User");

                participants.Add(new ParticipantDto
                {
                    UserId = participant.UserId,
                    FullName = displayName,
                    AvatarUrl = profile?.AvatarUrl,
                    Role = profile?.Role ?? "student",
                    StudentId = profile?.StudentId,
                    TutorId = profile?.TutorId,
                    AdminId = profile?.AdminId,
                    ConversationRole = Enum.Parse<ConversationRole>(participant.Role, true),
                    IsOnline = presence?.Status == PresenceStatus.Online,
                    LastSeenAt = presence?.LastSeenAt
                });
            }

            // Get last message
            var lastMessageResponse = await client
                .From<MessageEntity>()
                .Where(x => x.ConversationId == conversation.ConversationId)
                .Filter("deleted_at", Supabase.Postgrest.Constants.Operator.Is, (DateTime?)null)
                .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                .Limit(1)
                .Get();

            MessageDto? lastMessage = null;
            if (lastMessageResponse.Models.Any())
            {
                lastMessage = await BuildMessageDtoAsync(lastMessageResponse.Models.First(), currentUserId, client);
            }

            // Get unread count
            var unreadCount = await GetConversationUnreadCountInternalAsync(conversation.ConversationId, currentUserId, currentUserParticipant.LastReadAt, client);

            return new ConversationDto
            {
                ConversationId = conversation.ConversationId,
                ConversationType = conversation.ConversationType == "direct" ? ConversationType.Direct : ConversationType.Group,
                GroupName = conversation.GroupName,
                GroupDescription = conversation.GroupDescription,
                GroupAvatarUrl = conversation.GroupAvatarUrl,
                ParticipantCount = participants.Count,
                MaxParticipants = conversation.MaxParticipants,
                CreatedAt = conversation.CreatedAt,
                LastMessageAt = conversation.LastMessageAt,
                IsActive = conversation.IsActive,
                Participants = participants,
                LastMessage = lastMessage,
                UnreadCount = unreadCount,
                CurrentUserRole = Enum.Parse<ConversationRole>(currentUserParticipant.Role, true),
                IsMuted = currentUserParticipant.IsMuted
            };
        }

        private async Task<MessageDto?> BuildMessageDtoAsync(MessageEntity message, string currentUserId, Supabase.Client client)
        {
            var senderProfile = await GetUserProfileAsync(message.SenderUserId, client);

            // Determine sender name with better fallbacks
            string senderName;
            if (message.SenderUserId == currentUserId)
            {
                senderName = "You"; // Current user's messages show as "You"
            }
            else if (!string.IsNullOrWhiteSpace(senderProfile?.FullName))
            {
                senderName = senderProfile.FullName;
            }
            else if (senderProfile?.Role != null)
            {
                // Fallback: Show role with capitalization (e.g., "Student", "Tutor")
                senderName = char.ToUpper(senderProfile.Role[0]) + senderProfile.Role.Substring(1);
            }
            else
            {
                senderName = "User"; // Last resort
            }

            // Get read receipts
            var readsResponse = await client
                .From<MessageReadEntity>()
                .Where(x => x.MessageId == message.MessageId)
                .Get();

            var readReceipts = new List<ReadReceiptDto>();
            foreach (var read in readsResponse.Models)
            {
                var profile = await GetUserProfileAsync(read.UserId, client);
                var userName = !string.IsNullOrWhiteSpace(profile?.FullName)
                    ? profile.FullName
                    : (profile?.Role != null ? char.ToUpper(profile.Role[0]) + profile.Role.Substring(1) : "Unknown");

                readReceipts.Add(new ReadReceiptDto
                {
                    UserId = read.UserId,
                    UserName = userName,
                    ReadAt = read.ReadAt
                });
            }

            // Get reply-to message if exists
            MessageDto? replyToMessage = null;
            if (message.ReplyToMessageId.HasValue)
            {
                var replyMsg = await client
                    .From<MessageEntity>()
                    .Where(x => x.MessageId == message.ReplyToMessageId.Value)
                    .Single();

                if (replyMsg != null)
                {
                    var replyProfile = await GetUserProfileAsync(replyMsg.SenderUserId, client);
                    var replyName = !string.IsNullOrWhiteSpace(replyProfile?.FullName)
                        ? replyProfile.FullName
                        : (replyProfile?.Role != null ? char.ToUpper(replyProfile.Role[0]) + replyProfile.Role.Substring(1) : "Unknown");

                    replyToMessage = new MessageDto
                    {
                        MessageId = replyMsg.MessageId,
                        SenderId = replyMsg.SenderUserId,
                        SenderName = replyName,
                        Content = replyMsg.Content,
                        CreatedAt = replyMsg.CreatedAt
                    };
                }
            }

            return new MessageDto
            {
                MessageId = message.MessageId,
                ConversationId = message.ConversationId,
                SenderId = message.SenderUserId,
                SenderName = senderName,
                SenderRole = senderProfile?.Role ?? "system",
                SenderAvatar = senderProfile?.AvatarUrl,
                Content = message.Content,
                MessageType = Enum.Parse<MessageType>(message.MessageType, true),
                FileUrl = message.FileUrl,
                FileName = message.FileName,
                FileSize = message.FileSize,
                CreatedAt = message.CreatedAt,
                UpdatedAt = message.UpdatedAt,
                IsEdited = message.IsEdited,
                IsDeleted = message.DeletedAt != null,
                ReplyToMessageId = message.ReplyToMessageId,
                ReplyToMessage = replyToMessage,
                IsPinned = message.IsPinned,
                PinnedAt = message.PinnedAt,
                PinnedByUserId = message.PinnedByUserId,
                ReadBy = readReceipts,
                ReadCount = readReceipts.Count
            };
        }

        private async Task<PresenceDto?> GetUserPresenceInternalAsync(string userId, Supabase.Client client)
        {
            var presence = await client
                .From<UserPresenceEntity>()
                .Where(x => x.UserId == userId)
                .Single();

            if (presence == null)
                return null;

            return new PresenceDto
            {
                UserId = presence.UserId,
                Status = Enum.Parse<PresenceStatus>(presence.Status, true),
                LastSeenAt = presence.LastSeenAt
            };
        }

        private async Task<int> GetConversationUnreadCountInternalAsync(int conversationId, string userId, DateTime? lastReadAt, Supabase.Client client)
        {
            // Get messages created after last read time that user hasn't read and didn't send
            var messagesResponse = await client
                .From<MessageEntity>()
                .Where(x => x.ConversationId == conversationId && x.SenderUserId != userId)
                .Filter("deleted_at", Supabase.Postgrest.Constants.Operator.Is, (DateTime?)null)
                .Get();

            var messages = messagesResponse.Models;

            if (lastReadAt.HasValue)
            {
                messages = messages.Where(m => m.CreatedAt > lastReadAt.Value).ToList();
            }

            // Filter out messages that have been explicitly read
            var unreadMessages = new List<MessageEntity>();
            foreach (var message in messages)
            {
                var read = await client
                    .From<MessageReadEntity>()
                    .Where(x => x.MessageId == message.MessageId && x.UserId == userId)
                    .Single();

                if (read == null)
                {
                    unreadMessages.Add(message);
                }
            }

            return unreadMessages.Count;
        }

        #endregion
    }
}
