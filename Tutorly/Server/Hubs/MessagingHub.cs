using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using Tutorly.Server.Services;
using Tutorly.Shared;

namespace Tutorly.Server.Hubs
{
    [Authorize]
    public class MessagingHub : Hub
    {
        private readonly IMessagingService _messagingService;
        private readonly ILogger<MessagingHub> _logger;

        public MessagingHub(IMessagingService messagingService, ILogger<MessagingHub> logger)
        {
            _messagingService = messagingService;
            _logger = logger;
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
                    return;
                }

                _logger.LogInformation($"User {userId} connected to MessagingHub");

                // Update user presence to online
                await _messagingService.UpdatePresenceAsync(userId, PresenceStatus.Online);

                // Join all user's conversations
                await JoinUserConversationsAsync(userId);

                // Broadcast presence change to all connections
                await Clients.All.SendAsync("PresenceChanged", userId, PresenceStatus.Online.ToString(), DateTime.UtcNow);

                await base.OnConnectedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnConnectedAsync");
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {
                var userId = GetUserId();
                if (!string.IsNullOrEmpty(userId))
                {
                    _logger.LogInformation($"User {userId} disconnected from MessagingHub");

                    // Update user presence to offline
                    await _messagingService.UpdatePresenceAsync(userId, PresenceStatus.Offline);

                    // Stop any active typing indicators
                    await StopTypingInAllConversations(userId);

                    // Broadcast presence change
                    await Clients.All.SendAsync("PresenceChanged", userId, PresenceStatus.Offline.ToString(), DateTime.UtcNow);
                }

                await base.OnDisconnectedAsync(exception);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnDisconnectedAsync");
            }
        }

        #endregion

        #region Conversation Management

        /// <summary>
        /// Join a specific conversation room
        /// </summary>
        public async Task JoinConversation(int conversationId)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                    return;

                var groupName = GetConversationGroupName(conversationId);
                await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

                _logger.LogInformation($"User {userId} joined conversation {conversationId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error joining conversation {conversationId}");
            }
        }

        /// <summary>
        /// Leave a specific conversation room
        /// </summary>
        public async Task LeaveConversation(int conversationId)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                    return;

                var groupName = GetConversationGroupName(conversationId);
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

                // Stop typing if active
                await _messagingService.StopTypingAsync(conversationId, userId);

                _logger.LogInformation($"User {userId} left conversation {conversationId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error leaving conversation {conversationId}");
            }
        }

        #endregion

        #region Message Operations

        /// <summary>
        /// Send a message in a conversation (real-time)
        /// </summary>
        public async Task SendMessage(int conversationId, SendMessageDto dto)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                // Send message via service
                var result = await _messagingService.SendMessageAsync(conversationId, dto, userId);

                if (result.Success && result.Data != null)
                {
                    // Stop typing indicator for sender
                    await _messagingService.StopTypingAsync(conversationId, userId);

                    // Broadcast to conversation group
                    var groupName = GetConversationGroupName(conversationId);
                    await Clients.Group(groupName).SendAsync("ReceiveMessage", result.Data);

                    _logger.LogInformation($"Message sent in conversation {conversationId} by user {userId}");
                }
                else
                {
                    await Clients.Caller.SendAsync("Error", result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message");
                await Clients.Caller.SendAsync("Error", "Failed to send message");
            }
        }

        /// <summary>
        /// Edit a message (real-time notification)
        /// </summary>
        public async Task EditMessage(int messageId, string newContent)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                var dto = new EditMessageDto { Content = newContent };
                var result = await _messagingService.EditMessageAsync(messageId, dto, userId);

                if (result.Success && result.Data != null)
                {
                    // Broadcast to conversation group
                    var groupName = GetConversationGroupName(result.Data.ConversationId);
                    await Clients.Group(groupName).SendAsync("MessageEdited", result.Data);

                    _logger.LogInformation($"Message {messageId} edited by user {userId}");
                }
                else
                {
                    await Clients.Caller.SendAsync("Error", result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing message");
                await Clients.Caller.SendAsync("Error", "Failed to edit message");
            }
        }

        /// <summary>
        /// Delete a message (real-time notification)
        /// </summary>
        public async Task DeleteMessage(int messageId)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                // Get message first to know which conversation
                var messageResult = await _messagingService.GetMessageByIdAsync(messageId, userId);
                if (!messageResult.Success || messageResult.Data == null)
                {
                    await Clients.Caller.SendAsync("Error", "Message not found");
                    return;
                }

                var conversationId = messageResult.Data.ConversationId;
                var result = await _messagingService.DeleteMessageAsync(messageId, userId);

                if (result.Success)
                {
                    // Broadcast to conversation group
                    var groupName = GetConversationGroupName(conversationId);
                    await Clients.Group(groupName).SendAsync("MessageDeleted", messageId, conversationId);

                    _logger.LogInformation($"Message {messageId} deleted by user {userId}");
                }
                else
                {
                    await Clients.Caller.SendAsync("Error", result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message");
                await Clients.Caller.SendAsync("Error", "Failed to delete message");
            }
        }

        /// <summary>
        /// Pin a message (real-time notification)
        /// </summary>
        public async Task PinMessage(int messageId)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                var messageResult = await _messagingService.GetMessageByIdAsync(messageId, userId);
                if (!messageResult.Success || messageResult.Data == null)
                {
                    await Clients.Caller.SendAsync("Error", "Message not found");
                    return;
                }

                var conversationId = messageResult.Data.ConversationId;
                var result = await _messagingService.PinMessageAsync(messageId, userId);

                if (result.Success)
                {
                    var groupName = GetConversationGroupName(conversationId);
                    await Clients.Group(groupName).SendAsync("MessagePinned", messageId, conversationId, userId);
                }
                else
                {
                    await Clients.Caller.SendAsync("Error", result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pinning message");
                await Clients.Caller.SendAsync("Error", "Failed to pin message");
            }
        }

        /// <summary>
        /// Unpin a message (real-time notification)
        /// </summary>
        public async Task UnpinMessage(int messageId)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                var messageResult = await _messagingService.GetMessageByIdAsync(messageId, userId);
                if (!messageResult.Success || messageResult.Data == null)
                {
                    await Clients.Caller.SendAsync("Error", "Message not found");
                    return;
                }

                var conversationId = messageResult.Data.ConversationId;
                var result = await _messagingService.UnpinMessageAsync(messageId, userId);

                if (result.Success)
                {
                    var groupName = GetConversationGroupName(conversationId);
                    await Clients.Group(groupName).SendAsync("MessageUnpinned", messageId, conversationId);
                }
                else
                {
                    await Clients.Caller.SendAsync("Error", result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unpinning message");
                await Clients.Caller.SendAsync("Error", "Failed to unpin message");
            }
        }

        #endregion

        #region Read Receipts

        /// <summary>
        /// Mark messages as read (real-time notification)
        /// </summary>
        public async Task MarkMessagesAsRead(int conversationId, List<int> messageIds)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                    return;

                var dto = new MarkMessagesAsReadDto { MessageIds = messageIds };
                var result = await _messagingService.MarkMessagesAsReadAsync(conversationId, dto, userId);

                if (result.Success)
                {
                    // Broadcast to conversation group
                    var groupName = GetConversationGroupName(conversationId);
                    await Clients.Group(groupName).SendAsync("MessagesRead", conversationId, userId, messageIds, DateTime.UtcNow);

                    // Update unread count for user
                    var unreadResult = await _messagingService.GetConversationUnreadCountAsync(conversationId, userId);
                    if (unreadResult.Success)
                    {
                        await Clients.User(userId).SendAsync("UnreadCountChanged", conversationId, unreadResult.Data);
                    }

                    _logger.LogInformation($"User {userId} marked {messageIds.Count} messages as read in conversation {conversationId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as read");
            }
        }

        #endregion

        #region Typing Indicators

        /// <summary>
        /// Start typing in a conversation
        /// </summary>
        public async Task StartTyping(int conversationId)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                    return;

                await _messagingService.StartTypingAsync(conversationId, userId);

                // Get user profile for name
                var userProfile = await GetUserProfileAsync(userId);
                var userName = userProfile?.FullName ?? "Someone";
                var userRole = userProfile?.Role ?? "student";

                // Broadcast to others in conversation (exclude sender)
                var groupName = GetConversationGroupName(conversationId);
                await Clients.OthersInGroup(groupName).SendAsync("UserTyping", conversationId, userId, userName, userRole);

                _logger.LogDebug($"User {userId} started typing in conversation {conversationId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting typing");
            }
        }

        /// <summary>
        /// Stop typing in a conversation
        /// </summary>
        public async Task StopTyping(int conversationId)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                    return;

                await _messagingService.StopTypingAsync(conversationId, userId);

                // Broadcast to others in conversation
                var groupName = GetConversationGroupName(conversationId);
                await Clients.OthersInGroup(groupName).SendAsync("UserStoppedTyping", conversationId, userId);

                _logger.LogDebug($"User {userId} stopped typing in conversation {conversationId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping typing");
            }
        }

        #endregion

        #region Presence Management

        /// <summary>
        /// Update user's presence status
        /// </summary>
        public async Task UpdateStatus(PresenceStatus status)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                    return;

                await _messagingService.UpdatePresenceAsync(userId, status);

                // Broadcast to all connections
                await Clients.All.SendAsync("PresenceChanged", userId, status.ToString(), DateTime.UtcNow);

                _logger.LogInformation($"User {userId} updated status to {status}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating status");
            }
        }

        #endregion

        #region Group Events

        /// <summary>
        /// Notify when a user joins a group
        /// </summary>
        public async Task NotifyUserJoined(int conversationId, string joinedUserId)
        {
            try
            {
                var groupName = GetConversationGroupName(conversationId);
                await Clients.Group(groupName).SendAsync("ParticipantJoined", conversationId, joinedUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying user joined");
            }
        }

        /// <summary>
        /// Notify when a user leaves a group
        /// </summary>
        public async Task NotifyUserLeft(int conversationId, string leftUserId)
        {
            try
            {
                var groupName = GetConversationGroupName(conversationId);
                await Clients.Group(groupName).SendAsync("ParticipantLeft", conversationId, leftUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying user left");
            }
        }

        /// <summary>
        /// Notify when group details are updated
        /// </summary>
        public async Task NotifyGroupUpdated(int conversationId, string groupName, string? groupDescription)
        {
            try
            {
                var groupRoomName = GetConversationGroupName(conversationId);
                await Clients.Group(groupRoomName).SendAsync("GroupUpdated", conversationId, groupName, groupDescription);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying group updated");
            }
        }

        /// <summary>
        /// Notify when a user's role changes
        /// </summary>
        public async Task NotifyRoleChanged(int conversationId, string targetUserId, ConversationRole newRole)
        {
            try
            {
                var groupName = GetConversationGroupName(conversationId);
                await Clients.Group(groupName).SendAsync("ParticipantRoleChanged", conversationId, targetUserId, newRole.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying role changed");
            }
        }

        #endregion

        #region Helper Methods

        private string? GetUserId()
        {
            return Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }

        private static string GetConversationGroupName(int conversationId)
        {
            return $"Conversation_{conversationId}";
        }

        private async Task JoinUserConversationsAsync(string userId)
        {
            try
            {
                // Get all user's conversations
                var filter = new ConversationSearchDto { PageSize = 100 };
                var conversationsResult = await _messagingService.GetConversationsAsync(filter, userId);

                if (conversationsResult.Success && conversationsResult.Data != null)
                {
                    foreach (var conversation in conversationsResult.Data)
                    {
                        var groupName = GetConversationGroupName(conversation.ConversationId);
                        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
                    }

                    _logger.LogInformation($"User {userId} joined {conversationsResult.Data.Count} conversation groups");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error joining user conversations for user {userId}");
            }
        }

        private async Task StopTypingInAllConversations(string userId)
        {
            try
            {
                // Get all user's conversations and stop typing in each
                var filter = new ConversationSearchDto { PageSize = 100 };
                var conversationsResult = await _messagingService.GetConversationsAsync(filter, userId);

                if (conversationsResult.Success && conversationsResult.Data != null)
                {
                    foreach (var conversation in conversationsResult.Data)
                    {
                        await _messagingService.StopTypingAsync(conversation.ConversationId, userId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error stopping typing for user {userId}");
            }
        }

        private async Task<UnifiedProfileDto?> GetUserProfileAsync(string userId)
        {
            try
            {
                // Try to get user profile through the service
                // This is a simplified approach - you might want to cache this
                var searchResult = await _messagingService.SearchUsersAsync(new UserSearchDto { SearchQuery = "" }, userId);

                // For now, return a basic profile
                // In a real implementation, you'd want a dedicated method to get a single user's profile
                return new UnifiedProfileDto
                {
                    FullName = "User",
                    Role = "student"
                };
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }
}
