using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Components;
using Tutorly.Shared;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace Tutorly.Client.Services;

public class StudyRoomHubService : IAsyncDisposable
{
    private readonly NavigationManager _navigationManager;
    private readonly ILogger<StudyRoomHubService> _logger;
    private readonly StudyRoomWebRTCService _webrtcService;
    private readonly AuthenticationStateProvider _authStateProvider;

    private HubConnection? _hubConnection;
    private bool _isConnected = false;
    private Guid _currentRoomId;

    // Track participants in current room
    private readonly ConcurrentDictionary<Guid, StudyRoomParticipantDto> _participants = new();
    private Guid? _cachedUserId = null;

    // Events
    public event Func<StudyRoomParticipantDto, Task>? OnParticipantJoined;
    public event Func<Guid, Task>? OnParticipantLeft;
    public event Func<RoomChatMessageDto, Task>? OnChatMessageReceived;
    public event Func<Guid, bool, Task>? OnParticipantMuteChanged;
    public event Func<Guid, bool, Task>? OnParticipantScreenShareChanged;
    public event Func<Guid, bool, Task>? OnParticipantVideoChanged;

    // Call events
    public event Func<CallInvitationDto, Task>? OnCallInvitationReceived;
    public event Func<CallResponseDto, Task>? OnCallAccepted;
    public event Func<CallResponseDto, Task>? OnCallRejected;
    public event Func<Guid, Task>? OnCallCancelled;

    public StudyRoomHubService(
        NavigationManager navigationManager,
        ILogger<StudyRoomHubService> logger,
        StudyRoomWebRTCService webrtcService,
        AuthenticationStateProvider authStateProvider)
    {
        _navigationManager = navigationManager;
        _logger = logger;
        _webrtcService = webrtcService;
        _authStateProvider = authStateProvider;
    }

    public bool IsConnected => _isConnected && _hubConnection?.State == HubConnectionState.Connected;
    public IEnumerable<StudyRoomParticipantDto> Participants => _participants.Values;

    /// <summary>
    /// Connect to the StudyRoom SignalR Hub
    /// </summary>
    public async Task ConnectAsync(string accessToken)
    {
        // CRITICAL FIX: Only create new connection if we don't have one or it's disconnected
        if (_hubConnection != null && IsConnected)
        {
            Console.WriteLine("✅ StudyRoomHub already connected, reusing existing connection");
            return;
        }

        // Only disconnect if we have a connection that's not working
        if (_hubConnection != null)
        {
            Console.WriteLine("🔄 StudyRoomHub connection exists but not connected, reconnecting...");
            try
            {
                await DisconnectAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error during disconnect: {ex.Message}");
            }
        }

        try
        {
            var hubUrl = _navigationManager.ToAbsoluteUri("/studyroomHub");
            Console.WriteLine($"🔗 Creating new StudyRoomHub connection to: {hubUrl}");

            _hubConnection = new HubConnectionBuilder()
                .WithUrl($"{hubUrl}?access_token={accessToken}")
                .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
                .Build();

            // Handle reconnection events
            _hubConnection.Reconnecting += error =>
            {
                _logger.LogWarning("Connection lost. Attempting to reconnect...");
                _isConnected = false;
                return Task.CompletedTask;
            };

            _hubConnection.Reconnected += async connectionId =>
            {
                _logger.LogInformation("Reconnected to StudyRoom Hub");
                _isConnected = true;

                // Rejoin the room if we were in one
                if (_currentRoomId != Guid.Empty)
                {
                    try
                    {
                        await _hubConnection.InvokeAsync("JoinRoom", _currentRoomId, null);
                        _logger.LogInformation($"Automatically rejoined room {_currentRoomId} after reconnection");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to rejoin room after reconnection");
                    }
                }
            };

            _hubConnection.Closed += error =>
            {
                _logger.LogWarning("Connection closed");
                _isConnected = false;
                return Task.CompletedTask;
            };

            RegisterHandlers();

            await _hubConnection.StartAsync();
            _isConnected = true;

            // Log the user ID being used for this connection
            var currentUserId = await GetCurrentUserIdAsync();
            Console.WriteLine($"✅ StudyRoomHub connected successfully - User ID: {currentUserId}");
            Console.WriteLine($"✅ StudyRoomHub connection state: {_hubConnection.State}");
            Console.WriteLine($"✅ StudyRoomHub is connected: {IsConnected}");

            _logger.LogInformation($"Connected to StudyRoom Hub - User ID: {currentUserId}");

            // Ensure we're ready to receive call invitations
            Console.WriteLine("📞 StudyRoomHub: Ready to receive call invitations");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to StudyRoom Hub");
            Console.WriteLine($"❌ StudyRoomHub connection failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Disconnect from the hub
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_hubConnection != null)
        {
            try
            {
                await _hubConnection.StopAsync();
                await _hubConnection.DisposeAsync();
                _hubConnection = null;
                _isConnected = false;
                _logger.LogInformation("Disconnected from StudyRoom Hub");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disconnecting from StudyRoom Hub");
            }
        }
    }

    /// <summary>
    /// Join a study room
    /// </summary>
    public async Task<JoinRoomResponse> JoinRoomAsync(Guid roomId, string? roomCode = null)
    {
        Console.WriteLine($"🏠 ===== JOINING ROOM (SignalR) =====");
        Console.WriteLine($"🏠 Parameters: roomId={roomId}, roomCode={roomCode}");
        Console.WriteLine($"🏠 Current time: {DateTime.Now:HH:mm:ss.fff}");
        Console.WriteLine($"🏠 Hub connection state: {_hubConnection?.State}");
        Console.WriteLine($"🏠 Is connected: {IsConnected}");

        if (_hubConnection == null || !IsConnected)
        {
            Console.WriteLine($"❌ Not connected to hub - _hubConnection: {_hubConnection != null}, IsConnected: {IsConnected}");
            throw new InvalidOperationException("Not connected to hub");
        }

        try
        {
            _currentRoomId = roomId;
            Console.WriteLine($"🏠 Set current room ID: {_currentRoomId}");

            Console.WriteLine($"🏠 Invoking JoinRoom on hub...");
            var response = await _hubConnection.InvokeAsync<JoinRoomResponse>("JoinRoom", roomId, roomCode);
            Console.WriteLine($"🏠 JoinRoom response received:");
            Console.WriteLine($"🏠   Success: {response.Success}");
            Console.WriteLine($"🏠   Message: {response.Message}");
            Console.WriteLine($"🏠   Participants count: {response.CurrentParticipants?.Count ?? 0}");
            Console.WriteLine($"🏠   Room: {response.Room?.RoomName ?? "No room data"}");

            if (response.Success)
            {
                // Get current user ID
                Console.WriteLine($"🏠 Getting current user ID...");
                var currentUserId = await GetCurrentUserIdAsync();
                Console.WriteLine($"🏠 Current user ID: {currentUserId}");

                // Store participants
                Console.WriteLine($"🏠 Clearing existing participants and storing new ones...");
                _participants.Clear();
                foreach (var participant in response.CurrentParticipants)
                {
                    Console.WriteLine($"🏠 Storing participant: {participant.UserId} ({participant.UserName})");
                    _participants.TryAdd(participant.UserId, participant);

                    // Fire OnParticipantJoined event for existing participants (except current user)
                    if (participant.UserId != currentUserId && OnParticipantJoined != null)
                    {
                        Console.WriteLine($"🏠 Firing OnParticipantJoined event for existing participant: {participant.UserId}");
                        await OnParticipantJoined.Invoke(participant);
                    }
                }

                // Initialize WebRTC for existing participants
                Console.WriteLine($"🏠 Initializing WebRTC for existing participants...");
                await InitializeWebRTCForExistingParticipants(response.CurrentParticipants);

                Console.WriteLine($"✅ Successfully joined room {roomId} with {response.CurrentParticipants.Count} participants");
                _logger.LogInformation($"Joined room {roomId} with {response.CurrentParticipants.Count} participants");
            }
            else
            {
                Console.WriteLine($"❌ Failed to join room: {response.Message}");
            }

            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Exception in JoinRoomAsync:");
            Console.WriteLine($"❌   Message: {ex.Message}");
            Console.WriteLine($"❌   StackTrace: {ex.StackTrace}");
            Console.WriteLine($"❌   InnerException: {ex.InnerException?.Message}");
            _logger.LogError(ex, $"Error joining room {roomId}");
            return new JoinRoomResponse
            {
                Success = false,
                Message = $"Error joining room: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Reset hub service state for new call (without disconnecting)
    /// </summary>
    public async Task ResetForNewCallAsync()
    {
        try
        {
            Console.WriteLine("🔄 StudyRoomHubService: Resetting state for new call...");

            // Leave current room if we're in one
            if (_currentRoomId != Guid.Empty)
            {
                Console.WriteLine($"🏠 StudyRoomHubService: Leaving current room {_currentRoomId}...");
                try
                {
                    if (_hubConnection?.State == HubConnectionState.Connected)
                    {
                        await _hubConnection.InvokeAsync("LeaveRoom", _currentRoomId);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ StudyRoomHubService: Error leaving room: {ex.Message}");
                }
                _currentRoomId = Guid.Empty;
                Console.WriteLine("✅ StudyRoomHubService: Left current room");
            }

            // Clear participants
            _participants.Clear();
            Console.WriteLine("✅ StudyRoomHubService: Cleared participants");

            // Reset WebRTC state
            await _webrtcService.ResetForNewCallAsync();
            Console.WriteLine("✅ StudyRoomHubService: Reset WebRTC state");

            Console.WriteLine("✅ StudyRoomHubService: State reset complete for new call");
            _logger.LogInformation("Hub service state reset for new call");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ StudyRoomHubService: Error resetting state: {ex.Message}");
            _logger.LogError(ex, "Error resetting hub service state for new call");
        }
    }

    /// <summary>
    /// Leave the current study room
    /// </summary>
    public async Task LeaveRoomAsync()
    {
        if (_hubConnection == null || _currentRoomId == Guid.Empty)
        {
            return;
        }

        try
        {
            Console.WriteLine($"🏠 StudyRoomHubService: Leaving room {_currentRoomId}...");
            await _hubConnection.InvokeAsync("LeaveRoom", _currentRoomId);

            // Cleanup WebRTC connections
            await _webrtcService.CleanupAsync();

            _participants.Clear();
            _currentRoomId = Guid.Empty;

            Console.WriteLine("✅ StudyRoomHubService: Left room successfully");
            _logger.LogInformation("Left study room");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ StudyRoomHubService: Error leaving room: {ex.Message}");
            _logger.LogError(ex, "Error leaving room");
        }
    }

    /// <summary>
    /// Send chat message in room
    /// </summary>
    public async Task SendChatMessageAsync(string message)
    {
        if (_hubConnection == null || _currentRoomId == Guid.Empty)
        {
            throw new InvalidOperationException("Not in a room");
        }

        try
        {
            await _hubConnection.InvokeAsync("SendChatMessage", _currentRoomId, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending chat message");
            throw;
        }
    }

    /// <summary>
    /// Toggle mute status
    /// </summary>
    public async Task ToggleMuteAsync(bool isMuted)
    {
        if (_hubConnection == null || _currentRoomId == Guid.Empty)
        {
            return;
        }

        try
        {
            await _hubConnection.InvokeAsync("ToggleMute", _currentRoomId, isMuted);
            await _webrtcService.ToggleAudioAsync(!isMuted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling mute");
        }
    }

    /// <summary>
    /// Toggle screen share
    /// </summary>
    public async Task ToggleScreenShareAsync(bool isSharing)
    {
        if (_hubConnection == null || _currentRoomId == Guid.Empty)
        {
            return;
        }

        try
        {
            if (isSharing)
            {
                await _webrtcService.StartScreenShareAsync();
            }
            else
            {
                await _webrtcService.StopScreenShareAsync();
            }

            await _hubConnection.InvokeAsync("ToggleScreenShare", _currentRoomId, isSharing);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling screen share");
        }
    }

    /// <summary>
    /// Toggle video
    /// </summary>
    public async Task ToggleVideoAsync(bool isEnabled)
    {
        if (_hubConnection == null || _currentRoomId == Guid.Empty)
        {
            return;
        }

        try
        {
            await _webrtcService.ToggleVideoAsync(isEnabled);
            await _hubConnection.InvokeAsync("ToggleVideo", _currentRoomId, isEnabled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling video");
        }
    }

    #region Private Methods

    private void RegisterHandlers()
    {
        if (_hubConnection == null) return;

        // Participant joined
        _hubConnection.On<StudyRoomParticipantDto>("ParticipantJoined", async (participant) =>
        {
            try
            {
                _logger.LogInformation($"Participant joined: {participant.UserName}");
                _participants.TryAdd(participant.UserId, participant);

                // Create peer connection for new participant
                var currentUserId = await GetCurrentUserIdAsync();
                if (participant.UserId != currentUserId)
                {
                    await _webrtcService.CreatePeerConnectionAsync(participant.UserId, true);

                    // Create and send offer
                    var offer = await _webrtcService.CreateOfferAsync(participant.UserId);
                    if (offer.Success && _hubConnection != null)
                    {
                        await _hubConnection.InvokeAsync("SendOffer", _currentRoomId, participant.UserId, offer.Sdp);
                    }
                }

                if (OnParticipantJoined != null)
                {
                    await OnParticipantJoined.Invoke(participant);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling participant joined");
            }
        });

        // Participant left
        _hubConnection.On<Guid>("ParticipantLeft", async (userId) =>
        {
            try
            {
                _logger.LogInformation($"Participant left: {userId}");
                _participants.TryRemove(userId, out _);

                // Close peer connection
                await _webrtcService.ClosePeerConnectionAsync(userId);

                if (OnParticipantLeft != null)
                {
                    await OnParticipantLeft.Invoke(userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling participant left");
            }
        });

        // WebRTC Offer received
        _hubConnection.On<WebRTCSignalDto>("ReceiveOffer", async (signal) =>
        {
            try
            {
                Console.WriteLine($"📤 ===== RECEIVED OFFER (SignalR) =====");
                Console.WriteLine($"📤 Signal details: FromUserId={signal.FromUserId}, ToUserId={signal.ToUserId}");
                Console.WriteLine($"📤 SDP: {signal.Sdp?.Substring(0, Math.Min(100, signal.Sdp?.Length ?? 0))}...");
                Console.WriteLine($"📤 Current time: {DateTime.Now:HH:mm:ss.fff}");

                var currentUserId = await GetCurrentUserIdAsync();
                Console.WriteLine($"📤 Current user ID: {currentUserId}");

                if (signal.ToUserId != currentUserId)
                {
                    Console.WriteLine($"📤 Offer not for current user, ignoring");
                    return;
                }

                Console.WriteLine($"📤 Processing offer from {signal.FromUserId}");
                _logger.LogInformation($"Received offer from {signal.FromUserId}");

                // Create peer connection if doesn't exist
                Console.WriteLine($"📤 Creating peer connection for {signal.FromUserId}...");
                await _webrtcService.CreatePeerConnectionAsync(signal.FromUserId, false);
                Console.WriteLine($"📤 Peer connection created");

                // Create answer
                Console.WriteLine($"📤 Creating answer for {signal.FromUserId}...");
                var answer = await _webrtcService.CreateAnswerAsync(signal.FromUserId, signal.Sdp!);
                Console.WriteLine($"📤 Answer creation result:");
                Console.WriteLine($"📤   Success: {answer.Success}");
                Console.WriteLine($"📤   Pending: {answer.Pending}");
                Console.WriteLine($"📤   Error: {answer.Error}");

                if (answer.Success && _hubConnection != null)
                {
                    // Check if answer is pending (stored for later processing)
                    if (answer.Pending == true)
                    {
                        Console.WriteLine($"📤 Answer stored as pending for {signal.FromUserId}, will be processed when local stream is ready");
                        _logger.LogInformation($"Answer stored as pending for {signal.FromUserId}, will be processed when local stream is ready");
                    }
                    else
                    {
                        Console.WriteLine($"📤 Sending answer back to {signal.FromUserId}...");
                        await _hubConnection.InvokeAsync("SendAnswer", _currentRoomId, signal.FromUserId, answer.Sdp);
                        Console.WriteLine($"📤 Answer sent successfully");
                    }
                }
                else
                {
                    Console.WriteLine($"❌ Failed to create answer or hub connection not available");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Exception in ReceiveOffer handler:");
                Console.WriteLine($"❌   Message: {ex.Message}");
                Console.WriteLine($"❌   StackTrace: {ex.StackTrace}");
                Console.WriteLine($"❌   InnerException: {ex.InnerException?.Message}");
                _logger.LogError(ex, "Error handling WebRTC offer");
            }
        });

        // WebRTC Answer received
        _hubConnection.On<WebRTCSignalDto>("ReceiveAnswer", async (signal) =>
        {
            try
            {
                Console.WriteLine($"📥 ===== RECEIVED ANSWER (SignalR) =====");
                Console.WriteLine($"📥 Signal details: FromUserId={signal.FromUserId}, ToUserId={signal.ToUserId}");
                Console.WriteLine($"📥 SDP: {signal.Sdp?.Substring(0, Math.Min(100, signal.Sdp?.Length ?? 0))}...");
                Console.WriteLine($"📥 Current time: {DateTime.Now:HH:mm:ss.fff}");

                var currentUserId = await GetCurrentUserIdAsync();
                Console.WriteLine($"📥 Current user ID: {currentUserId}");

                if (signal.ToUserId != currentUserId)
                {
                    Console.WriteLine($"📥 Answer not for current user, ignoring");
                    return;
                }

                Console.WriteLine($"📥 Processing answer from {signal.FromUserId}");
                _logger.LogInformation($"Received answer from {signal.FromUserId}");

                Console.WriteLine($"📥 Setting remote description...");
                await _webrtcService.SetRemoteDescriptionAsync(signal.FromUserId, signal.Sdp!);
                Console.WriteLine($"📥 Remote description set successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Exception in ReceiveAnswer handler:");
                Console.WriteLine($"❌   Message: {ex.Message}");
                Console.WriteLine($"❌   StackTrace: {ex.StackTrace}");
                Console.WriteLine($"❌   InnerException: {ex.InnerException?.Message}");
                _logger.LogError(ex, "Error handling WebRTC answer");
            }
        });

        // Handle answer created from pending offers
        _webrtcService.OnAnswerCreated += async (userId, answerSdp) =>
        {
            try
            {
                if (_hubConnection != null && _currentRoomId != Guid.Empty)
                {
                    await _hubConnection.InvokeAsync("SendAnswer", _currentRoomId, userId, answerSdp);
                    _logger.LogInformation($"Sent answer for pending offer to {userId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending answer for pending offer");
            }
        };

        // ICE Candidate received
        _hubConnection.On<WebRTCSignalDto>("ReceiveIceCandidate", async (signal) =>
        {
            try
            {
                var currentUserId = await GetCurrentUserIdAsync();
                if (signal.ToUserId != currentUserId)
                    return;

                var candidate = new IceCandidateData
                {
                    Candidate = signal.Candidate,
                    SdpMid = signal.SdpMid,
                    SdpMLineIndex = signal.SdpMLineIndex
                };

                await _webrtcService.AddIceCandidateAsync(signal.FromUserId, candidate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling ICE candidate");
            }
        });

        // Chat message received
        _hubConnection.On<RoomChatMessageDto>("ReceiveChatMessage", async (message) =>
        {
            try
            {
                _logger.LogInformation($"Chat message from {message.UserName}: {message.Message}");
                if (OnChatMessageReceived != null)
                {
                    await OnChatMessageReceived.Invoke(message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling chat message");
            }
        });

        // Participant mute changed
        _hubConnection.On<Guid, bool>("ParticipantMuteChanged", async (userId, isMuted) =>
        {
            try
            {
                if (_participants.TryGetValue(userId, out var participant))
                {
                    participant.IsMuted = isMuted;
                }

                if (OnParticipantMuteChanged != null)
                {
                    await OnParticipantMuteChanged.Invoke(userId, isMuted);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling mute changed");
            }
        });

        // Participant screen share changed
        _hubConnection.On<Guid, bool>("ParticipantScreenShareChanged", async (userId, isSharing) =>
        {
            try
            {
                if (_participants.TryGetValue(userId, out var participant))
                {
                    participant.IsScreenSharing = isSharing;
                }

                if (OnParticipantScreenShareChanged != null)
                {
                    await OnParticipantScreenShareChanged.Invoke(userId, isSharing);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling screen share changed");
            }
        });

        // Participant video changed
        _hubConnection.On<Guid, bool>("ParticipantVideoChanged", async (userId, isEnabled) =>
        {
            try
            {
                if (OnParticipantVideoChanged != null)
                {
                    await OnParticipantVideoChanged.Invoke(userId, isEnabled);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling video changed");
            }
        });

        // Handle ICE candidates generated locally
        _webrtcService.OnIceCandidateGenerated += async (userId, candidate) =>
        {
            try
            {
                if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
                {
                    await _hubConnection.InvokeAsync("SendIceCandidate",
                        _currentRoomId,
                        userId,
                        candidate.Candidate,
                        candidate.SdpMid,
                        candidate.SdpMLineIndex);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending ICE candidate");
            }
        };

        // Room ended (all participants left)
        _hubConnection.On<string>("RoomEnded", (message) =>
        {
            try
            {
                _logger.LogInformation($"Room ended: {message}");

                // Clear all participants
                _participants.Clear();

                // Only navigate if we're currently in a room session
                var currentPath = _navigationManager.Uri;
                if (currentPath.Contains("/calendar/session/"))
                {
                    _navigationManager.NavigateTo("/calendar/book");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling room ended");
            }
        });

        // Call invitation received
        _hubConnection.On<CallInvitationDto>("ReceiveCallInvitation", async (invitation) =>
        {
            try
            {
                _logger.LogInformation($"Incoming call from {invitation.FromUserName}");
                if (OnCallInvitationReceived != null)
                {
                    await OnCallInvitationReceived.Invoke(invitation);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling call invitation");
            }
        });

        // Call accepted
        _hubConnection.On<CallResponseDto>("CallAccepted", async (response) =>
        {
            try
            {
                _logger.LogInformation($"Call accepted");
                if (OnCallAccepted != null)
                {
                    await OnCallAccepted.Invoke(response);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling call accepted");
            }
        });

        // Call rejected
        _hubConnection.On<CallResponseDto>("CallRejected", async (response) =>
        {
            try
            {
                _logger.LogInformation($"Call rejected");
                if (OnCallRejected != null)
                {
                    await OnCallRejected.Invoke(response);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling call rejected");
            }
        });

        // Call cancelled
        _hubConnection.On<Guid>("CallCancelled", async (callId) =>
        {
            try
            {
                _logger.LogInformation($"Call cancelled");
                if (OnCallCancelled != null)
                {
                    await OnCallCancelled.Invoke(callId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling call cancelled");
            }
        });
    }

    private async Task InitializeWebRTCForExistingParticipants(List<StudyRoomParticipantDto> participants)
    {
        // Get current user ID from authentication
        var currentUserId = await GetCurrentUserIdAsync();

        foreach (var participant in participants)
        {
            if (participant.UserId != currentUserId)
            {
                try
                {
                    await _webrtcService.CreatePeerConnectionAsync(participant.UserId, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error initializing WebRTC for participant {participant.UserId}");
                }
            }
        }
    }

    private async Task<Guid> GetCurrentUserIdAsync()
    {
        try
        {
            // Return cached user ID if available
            if (_cachedUserId.HasValue)
            {
                return _cachedUserId.Value;
            }

            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            var userIdClaim = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? authState.User.FindFirst("sub")?.Value;

            Console.WriteLine($"🔍 GetCurrentUserIdAsync: userIdClaim = {userIdClaim}");
            Console.WriteLine($"🔍 GetCurrentUserIdAsync: IsAuthenticated = {authState.User.Identity?.IsAuthenticated}");
            Console.WriteLine($"🔍 GetCurrentUserIdAsync: Name = {authState.User.Identity?.Name}");

            if (Guid.TryParse(userIdClaim, out var userId))
            {
                Console.WriteLine($"✅ GetCurrentUserIdAsync: Found user ID from claims: {userId}");
                _cachedUserId = userId; // Cache the user ID
                return userId;
            }

            // Fallback: try to get from participants
            var currentParticipant = _participants.Values.FirstOrDefault();
            if (currentParticipant != null)
            {
                Console.WriteLine($"⚠️ GetCurrentUserIdAsync: Using fallback user ID from participants: {currentParticipant.UserId}");
                _cachedUserId = currentParticipant.UserId; // Cache the user ID
                return currentParticipant.UserId;
            }

            Console.WriteLine("❌ GetCurrentUserIdAsync: No user ID found");
            return Guid.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user ID");
            return Guid.Empty;
        }
    }

    #endregion

    #region WebRTC Signaling Methods

    public async Task SendIceCandidateAsync(Guid roomId, string userId, object candidate)
    {
        Console.WriteLine($"🔗 SendIceCandidateAsync: roomId={roomId}, userId={userId}");
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            // Parse the candidate object to extract the required fields
            var candidateData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(candidate.ToString() ?? "{}");

            var candidateStr = candidateData?.GetValueOrDefault("candidate")?.ToString() ?? "";
            var sdpMid = candidateData?.GetValueOrDefault("sdpMid")?.ToString();
            var sdpMLineIndex = candidateData?.GetValueOrDefault("sdpMLineIndex")?.ToString();

            Console.WriteLine($"🔗 SendIceCandidateAsync: candidate={candidateStr}, sdpMid={sdpMid}, sdpMLineIndex={sdpMLineIndex}");

            int? sdpMLineIndexInt = null;
            if (int.TryParse(sdpMLineIndex, out var sdpMLineIndexValue))
            {
                sdpMLineIndexInt = sdpMLineIndexValue;
            }

            await _hubConnection.SendAsync("SendIceCandidate", roomId, Guid.Parse(userId), candidateStr, sdpMid, sdpMLineIndexInt);
            Console.WriteLine($"✅ SendIceCandidateAsync: Sent successfully");
        }
        else
        {
            Console.WriteLine($"❌ SendIceCandidateAsync: Hub not connected. State = {_hubConnection?.State}");
        }
    }

    public async Task SendWebRTCOfferAsync(Guid roomId, string userId, object offer)
    {
        Console.WriteLine($"📤 SendWebRTCOfferAsync: roomId={roomId}, userId={userId}");
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await _hubConnection.SendAsync("SendOffer", roomId, Guid.Parse(userId), offer.ToString());
            Console.WriteLine($"✅ SendWebRTCOfferAsync: Sent successfully");
        }
        else
        {
            Console.WriteLine($"❌ SendWebRTCOfferAsync: Hub not connected. State = {_hubConnection?.State}");
        }
    }

    public async Task SendWebRTCAnswerAsync(Guid roomId, string userId, object answer)
    {
        Console.WriteLine($"📥 SendWebRTCAnswerAsync: roomId={roomId}, userId={userId}");
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await _hubConnection.SendAsync("SendAnswer", roomId, Guid.Parse(userId), answer.ToString());
            Console.WriteLine($"✅ SendWebRTCAnswerAsync: Sent successfully");
        }
        else
        {
            Console.WriteLine($"❌ SendWebRTCAnswerAsync: Hub not connected. State = {_hubConnection?.State}");
        }
    }

    #endregion

    #region Chat Methods

    public async Task SendChatMessageAsync(Guid roomId, string message)
    {
        Console.WriteLine($"💬 SendChatMessageAsync: roomId={roomId}, message='{message}', connectionState={_hubConnection?.State}");

        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            try
            {
                await _hubConnection.SendAsync("SendChatMessage", roomId, message);
                Console.WriteLine($"✅ SendChatMessageAsync: Message sent successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ SendChatMessageAsync: Error sending message: {ex.Message}");
                throw;
            }
        }
        else
        {
            Console.WriteLine($"❌ SendChatMessageAsync: Hub not connected. State = {_hubConnection?.State}");
            throw new InvalidOperationException("Hub not connected");
        }
    }

    public async Task<List<RoomChatMessageDto>> GetChatHistoryAsync(Guid roomId)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            return await _hubConnection.InvokeAsync<List<RoomChatMessageDto>>(
                "GetRoomChatHistory", roomId
            );
        }
        return new List<RoomChatMessageDto>();
    }

    #endregion

    #region Call Methods

    public async Task InitiateCallAsync(int conversationId, Guid toUserId, CallType callType)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await _hubConnection.SendAsync("InitiateCall", conversationId, toUserId, callType);
        }
    }

    public async Task RespondToCallAsync(CallResponseDto response)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await _hubConnection.SendAsync("RespondToCall", response);
        }
    }

    public async Task CancelCallAsync(Guid callId, Guid roomId)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await _hubConnection.SendAsync("CancelCall", callId, roomId);
        }
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        try
        {
            await LeaveRoomAsync();
            await DisconnectAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing StudyRoomHubService");
        }
    }
}

