using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Tutorly.Shared;

namespace Tutorly.Client.Services
{
    /// <summary>
    /// SignalR service for real-time messaging functionality.
    /// Connects to the MessagingHub on the server and handles all real-time events.
    /// </summary>
    public class MessagingHubService : IAsyncDisposable
    {
        private HubConnection? _hubConnection;
        private readonly NavigationManager _navigationManager;
        private readonly IJSRuntime _jsRuntime;
        private bool _isInitialized = false;

        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
        public bool IsConnecting => _hubConnection?.State == HubConnectionState.Connecting || _hubConnection?.State == HubConnectionState.Reconnecting;

        #region Events

        public event Action<MessageDto>? OnMessageReceived;
        public event Action<MessageDto>? OnMessageEdited;
        public event Action<int, int>? OnMessageDeleted;
        public event Action<int, int, string>? OnMessagePinned;
        public event Action<int, int>? OnMessageUnpinned;
        public event Action<int, string, List<int>, DateTime>? OnMessagesRead;
        public event Action<int, string, string, string>? OnUserTyping;
        public event Action<int, string>? OnUserStoppedTyping;
        public event Action<string, string, DateTime>? OnPresenceChanged;
        public event Action<int, string>? OnParticipantJoined;
        public event Action<int, string>? OnParticipantLeft;
        public event Action<int, string, string?>? OnGroupUpdated;
        public event Action<int, string, string>? OnParticipantRoleChanged;
        public event Action<int, int>? OnUnreadCountChanged;
        public event Action<string>? OnError;
        public event Action? OnConnected;
        public event Action? OnDisconnected;
        public event Action? OnReconnecting;
        public event Action? OnReconnected;

        #endregion

        public MessagingHubService(NavigationManager navigationManager, IJSRuntime jsRuntime)
        {
            _navigationManager = navigationManager;
            _jsRuntime = jsRuntime;
        }

        /// <summary>
        /// Initialize and start the SignalR connection
        /// </summary>
        public async Task StartAsync()
        {
            if (_isInitialized)
                return;

            try
            {
                // Get JWT token from localStorage (same approach as JwtHttpClient)
                var token = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "accessToken");
                if (string.IsNullOrEmpty(token))
                {
                    Console.WriteLine("MessagingHub: No access token available in localStorage");
                    return;
                }

                // Build hub URL (adjust based on your setup)
                var hubUrl = _navigationManager.ToAbsoluteUri("/messagingHub");

                _hubConnection = new HubConnectionBuilder()
                    .WithUrl(hubUrl, options =>
                    {
                        options.AccessTokenProvider = () => Task.FromResult(token)!;
                    })
                    .WithAutomaticReconnect(new[]
                    {
                        TimeSpan.Zero,
                        TimeSpan.FromSeconds(2),
                        TimeSpan.FromSeconds(5),
                        TimeSpan.FromSeconds(10),
                        TimeSpan.FromSeconds(30)
                    })
                    .Build();

                // Register all event handlers
                RegisterEventHandlers();

                // Connection lifecycle handlers
                _hubConnection.Reconnecting += error =>
                {
                    Console.WriteLine($"MessagingHub: Reconnecting... {error?.Message}");
                    OnReconnecting?.Invoke();
                    return Task.CompletedTask;
                };

                _hubConnection.Reconnected += connectionId =>
                {
                    Console.WriteLine($"MessagingHub: Reconnected! ConnectionId: {connectionId}");
                    OnReconnected?.Invoke();
                    return Task.CompletedTask;
                };

                _hubConnection.Closed += error =>
                {
                    Console.WriteLine($"MessagingHub: Connection closed. {error?.Message}");
                    OnDisconnected?.Invoke();
                    return Task.CompletedTask;
                };

                // Start connection
                await _hubConnection.StartAsync();
                _isInitialized = true;
                Console.WriteLine("MessagingHub: Connected successfully!");
                OnConnected?.Invoke();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MessagingHub: Failed to connect - {ex.Message}");
                throw;
            }
        }

        private void RegisterEventHandlers()
        {
            if (_hubConnection == null) return;

            _hubConnection.On<MessageDto>("ReceiveMessage", message =>
            {
                Console.WriteLine($"MessagingHub: New message received in conversation {message.ConversationId}");
                OnMessageReceived?.Invoke(message);
            });

            _hubConnection.On<MessageDto>("MessageEdited", message =>
            {
                Console.WriteLine($"MessagingHub: Message {message.MessageId} edited");
                OnMessageEdited?.Invoke(message);
            });

            _hubConnection.On<int, int>("MessageDeleted", (messageId, conversationId) =>
            {
                Console.WriteLine($"MessagingHub: Message {messageId} deleted");
                OnMessageDeleted?.Invoke(messageId, conversationId);
            });

            _hubConnection.On<int, int, string>("MessagePinned", (messageId, conversationId, userId) =>
            {
                Console.WriteLine($"MessagingHub: Message {messageId} pinned");
                OnMessagePinned?.Invoke(messageId, conversationId, userId);
            });

            _hubConnection.On<int, int>("MessageUnpinned", (messageId, conversationId) =>
            {
                Console.WriteLine($"MessagingHub: Message {messageId} unpinned");
                OnMessageUnpinned?.Invoke(messageId, conversationId);
            });

            _hubConnection.On<int, string, List<int>, DateTime>("MessagesRead", (conversationId, userId, messageIds, readAt) =>
            {
                Console.WriteLine($"MessagingHub: {messageIds.Count} messages read by {userId}");
                OnMessagesRead?.Invoke(conversationId, userId, messageIds, readAt);
            });

            _hubConnection.On<int, string, string, string>("UserTyping", (conversationId, userId, userName, userRole) =>
            {
                OnUserTyping?.Invoke(conversationId, userId, userName, userRole);
            });

            _hubConnection.On<int, string>("UserStoppedTyping", (conversationId, userId) =>
            {
                OnUserStoppedTyping?.Invoke(conversationId, userId);
            });

            _hubConnection.On<string, string, DateTime>("PresenceChanged", (userId, status, timestamp) =>
            {
                Console.WriteLine($"MessagingHub: User {userId} is now {status}");
                OnPresenceChanged?.Invoke(userId, status, timestamp);
            });

            _hubConnection.On<int, string>("ParticipantJoined", (conversationId, userId) =>
            {
                Console.WriteLine($"MessagingHub: User {userId} joined conversation {conversationId}");
                OnParticipantJoined?.Invoke(conversationId, userId);
            });

            _hubConnection.On<int, string>("ParticipantLeft", (conversationId, userId) =>
            {
                Console.WriteLine($"MessagingHub: User {userId} left conversation {conversationId}");
                OnParticipantLeft?.Invoke(conversationId, userId);
            });

            _hubConnection.On<int, string, string?>("GroupUpdated", (conversationId, groupName, groupDescription) =>
            {
                Console.WriteLine($"MessagingHub: Group {conversationId} updated");
                OnGroupUpdated?.Invoke(conversationId, groupName, groupDescription);
            });

            _hubConnection.On<int, string, string>("ParticipantRoleChanged", (conversationId, userId, role) =>
            {
                Console.WriteLine($"MessagingHub: User {userId} role changed to {role}");
                OnParticipantRoleChanged?.Invoke(conversationId, userId, role);
            });

            _hubConnection.On<int, int>("UnreadCountChanged", (conversationId, unreadCount) =>
            {
                OnUnreadCountChanged?.Invoke(conversationId, unreadCount);
            });

            _hubConnection.On<string>("Error", error =>
            {
                Console.WriteLine($"MessagingHub: Error - {error}");
                OnError?.Invoke(error);
            });
        }

        #region Hub Methods

        public async Task JoinConversationAsync(int conversationId)
        {
            if (_hubConnection?.State == HubConnectionState.Connected)
            {
                await _hubConnection.SendAsync("JoinConversation", conversationId);
                Console.WriteLine($"MessagingHub: Joined conversation {conversationId}");
            }
        }

        public async Task LeaveConversationAsync(int conversationId)
        {
            if (_hubConnection?.State == HubConnectionState.Connected)
            {
                await _hubConnection.SendAsync("LeaveConversation", conversationId);
                Console.WriteLine($"MessagingHub: Left conversation {conversationId}");
            }
        }

        public async Task SendMessageAsync(int conversationId, SendMessageDto dto)
        {
            if (_hubConnection?.State == HubConnectionState.Connected)
            {
                await _hubConnection.SendAsync("SendMessage", conversationId, dto);
            }
        }

        public async Task EditMessageAsync(int messageId, string newContent)
        {
            if (_hubConnection?.State == HubConnectionState.Connected)
            {
                await _hubConnection.SendAsync("EditMessage", messageId, newContent);
            }
        }

        public async Task DeleteMessageAsync(int messageId)
        {
            if (_hubConnection?.State == HubConnectionState.Connected)
            {
                await _hubConnection.SendAsync("DeleteMessage", messageId);
            }
        }

        public async Task PinMessageAsync(int messageId)
        {
            if (_hubConnection?.State == HubConnectionState.Connected)
            {
                await _hubConnection.SendAsync("PinMessage", messageId);
            }
        }

        public async Task UnpinMessageAsync(int messageId)
        {
            if (_hubConnection?.State == HubConnectionState.Connected)
            {
                await _hubConnection.SendAsync("UnpinMessage", messageId);
            }
        }

        public async Task MarkMessagesAsReadAsync(int conversationId, List<int> messageIds)
        {
            if (_hubConnection?.State == HubConnectionState.Connected)
            {
                await _hubConnection.SendAsync("MarkMessagesAsRead", conversationId, messageIds);
            }
        }

        public async Task StartTypingAsync(int conversationId)
        {
            if (_hubConnection?.State == HubConnectionState.Connected)
            {
                await _hubConnection.SendAsync("StartTyping", conversationId);
            }
        }

        public async Task StopTypingAsync(int conversationId)
        {
            if (_hubConnection?.State == HubConnectionState.Connected)
            {
                await _hubConnection.SendAsync("StopTyping", conversationId);
            }
        }

        public async Task UpdateStatusAsync(PresenceStatus status)
        {
            if (_hubConnection?.State == HubConnectionState.Connected)
            {
                await _hubConnection.SendAsync("UpdateStatus", status);
            }
        }

        #endregion

        public async ValueTask DisposeAsync()
        {
            if (_hubConnection != null)
            {
                await _hubConnection.DisposeAsync();
                _isInitialized = false;
            }
        }
    }
}

