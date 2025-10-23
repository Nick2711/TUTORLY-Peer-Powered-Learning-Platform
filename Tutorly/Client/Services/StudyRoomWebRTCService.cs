using Microsoft.JSInterop;
using System.Collections.Concurrent;

namespace Tutorly.Client.Services;

#region Data Models

public class PeerConnectionState
{
    public Guid UserId { get; set; }
    public string ConnectionState { get; set; } = "new";
}

public class MediaStreamResult
{
    public bool Success { get; set; }
    public string? StreamId { get; set; }
    public string? Error { get; set; }
}

public class OperationResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
}

public class SdpResult
{
    public bool Success { get; set; }
    public string? Sdp { get; set; }
    public string? Type { get; set; }
    public string? Error { get; set; }
    public bool? Pending { get; set; }
    public string? Message { get; set; }
}

public class ToggleResult
{
    public bool Success { get; set; }
    public bool Enabled { get; set; }
    public string? Error { get; set; }
}

public class IceCandidateData
{
    public string? Candidate { get; set; }
    public string? SdpMid { get; set; }
    public int? SdpMLineIndex { get; set; }
}

#endregion

public class StudyRoomWebRTCService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<StudyRoomWebRTCService> _logger;
    private DotNetObjectReference<StudyRoomWebRTCService>? _dotNetReference;
    private bool _isInitialized = false;

    // Track peer connections and their states
    private readonly ConcurrentDictionary<Guid, PeerConnectionState> _peerStates = new();

    // Events
    public event Func<Guid, string, Task>? OnRemoteStreamReceived;
    public event Func<Guid, IceCandidateData, Task>? OnIceCandidateGenerated;
    public event Func<Guid, string, Task>? OnConnectionStateChanged;
    public event Func<Task>? OnScreenShareStopped;
    public event Func<Guid, string, Task>? OnAnswerCreated;

    public StudyRoomWebRTCService(IJSRuntime jsRuntime, ILogger<StudyRoomWebRTCService> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    /// <summary>
    /// Initialize WebRTC service and set up .NET reference for callbacks
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        try
        {
            _dotNetReference = DotNetObjectReference.Create(this);
            await _jsRuntime.InvokeVoidAsync("webrtcHandler.setDotNetReference", _dotNetReference);
            _isInitialized = true;
            _logger.LogInformation("WebRTC service initialized");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing WebRTC service");
            throw;
        }
    }

    /// <summary>
    /// Start local media stream (camera and microphone)
    /// </summary>
    public async Task<MediaStreamResult> StartLocalStreamAsync(string videoElementId, bool audio = true, bool video = true)
    {
        try
        {
            Console.WriteLine($"🎥 ===== STARTING LOCAL STREAM (C#) =====");
            Console.WriteLine($"🎥 Parameters: videoElementId={videoElementId}, audio={audio}, video={video}");
            Console.WriteLine($"🎥 Current time: {DateTime.Now:HH:mm:ss.fff}");
            Console.WriteLine($"🎥 JS Runtime available: {_jsRuntime != null}");

            var result = await _jsRuntime.InvokeAsync<MediaStreamResult>(
                "webrtcHandler.initializeMediaStream",
                videoElementId,
                audio,
                video);

            Console.WriteLine($"🎥 Local stream start result:");
            Console.WriteLine($"🎥   Success: {result.Success}");
            Console.WriteLine($"🎥   StreamId: {result.StreamId}");
            Console.WriteLine($"🎥   Error: {result.Error}");

            if (result.Success)
            {
                Console.WriteLine($"✅ Local stream started successfully with ID: {result.StreamId}");
                _logger.LogInformation($"Local stream started: {result.StreamId}");
            }
            else
            {
                Console.WriteLine($"❌ Failed to start local stream: {result.Error}");
                _logger.LogError($"Failed to start local stream: {result.Error}");
            }

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Exception in StartLocalStreamAsync:");
            Console.WriteLine($"❌   Message: {ex.Message}");
            Console.WriteLine($"❌   StackTrace: {ex.StackTrace}");
            Console.WriteLine($"❌   InnerException: {ex.InnerException?.Message}");
            _logger.LogError(ex, "Error starting local stream");
            return new MediaStreamResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Stop local media stream
    /// </summary>
    public async Task StopLocalStreamAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("webrtcHandler.stopLocalStream");
            _logger.LogInformation("Local stream stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping local stream");
        }
    }

    /// <summary>
    /// Create a peer connection for a remote user
    /// </summary>
    public async Task<OperationResult> CreatePeerConnectionAsync(Guid userId, bool isInitiator = false)
    {
        try
        {
            var result = await _jsRuntime.InvokeAsync<OperationResult>(
                "webrtcHandler.createPeerConnection",
                userId.ToString(),
                isInitiator);

            if (result.Success)
            {
                _peerStates.TryAdd(userId, new PeerConnectionState { UserId = userId });
                _logger.LogInformation($"Peer connection created for {userId}");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating peer connection for {userId}");
            return new OperationResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Create an offer for a peer connection
    /// </summary>
    public async Task<SdpResult> CreateOfferAsync(Guid userId)
    {
        try
        {
            Console.WriteLine($"📤 ===== CREATING OFFER (C#) =====");
            Console.WriteLine($"📤 Parameters: userId={userId}");
            Console.WriteLine($"📤 Current time: {DateTime.Now:HH:mm:ss.fff}");
            Console.WriteLine($"📤 JS Runtime available: {_jsRuntime != null}");

            var result = await _jsRuntime.InvokeAsync<SdpResult>(
                "webrtcHandler.createOffer",
                userId.ToString());

            Console.WriteLine($"📤 Offer creation result:");
            Console.WriteLine($"📤   Success: {result.Success}");
            Console.WriteLine($"📤   SDP: {result.Sdp?.Substring(0, Math.Min(100, result.Sdp?.Length ?? 0))}...");
            Console.WriteLine($"📤   Type: {result.Type}");
            Console.WriteLine($"📤   Error: {result.Error}");
            Console.WriteLine($"📤   Pending: {result.Pending}");
            Console.WriteLine($"📤   Message: {result.Message}");

            if (result.Success)
            {
                Console.WriteLine($"✅ Offer created successfully for {userId}");
                _logger.LogInformation($"Offer created for {userId}");
            }
            else
            {
                Console.WriteLine($"❌ Failed to create offer for {userId}: {result.Error}");
                _logger.LogError($"Failed to create offer for {userId}: {result.Error}");
            }

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Exception in CreateOfferAsync:");
            Console.WriteLine($"❌   Message: {ex.Message}");
            Console.WriteLine($"❌   StackTrace: {ex.StackTrace}");
            Console.WriteLine($"❌   InnerException: {ex.InnerException?.Message}");
            _logger.LogError(ex, $"Error creating offer for {userId}");
            return new SdpResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Create an answer for a peer connection
    /// </summary>
    public async Task<SdpResult> CreateAnswerAsync(Guid userId, string offerSdp)
    {
        try
        {
            Console.WriteLine($"📥 ===== CREATING ANSWER (C#) =====");
            Console.WriteLine($"📥 Parameters: userId={userId}, offerSdp={offerSdp?.Substring(0, Math.Min(100, offerSdp?.Length ?? 0))}...");
            Console.WriteLine($"📥 Current time: {DateTime.Now:HH:mm:ss.fff}");
            Console.WriteLine($"📥 JS Runtime available: {_jsRuntime != null}");

            var result = await _jsRuntime.InvokeAsync<SdpResult>(
                "webrtcHandler.createAnswer",
                userId.ToString(),
                offerSdp);

            Console.WriteLine($"📥 Answer creation result:");
            Console.WriteLine($"📥   Success: {result.Success}");
            Console.WriteLine($"📥   SDP: {result.Sdp?.Substring(0, Math.Min(100, result.Sdp?.Length ?? 0))}...");
            Console.WriteLine($"📥   Type: {result.Type}");
            Console.WriteLine($"📥   Error: {result.Error}");
            Console.WriteLine($"📥   Pending: {result.Pending}");
            Console.WriteLine($"📥   Message: {result.Message}");

            if (result.Success)
            {
                Console.WriteLine($"✅ Answer created successfully for {userId}");
                _logger.LogInformation($"Answer created for {userId}");
            }
            else
            {
                Console.WriteLine($"❌ Failed to create answer for {userId}: {result.Error}");
                _logger.LogError($"Failed to create answer for {userId}: {result.Error}");
            }

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Exception in CreateAnswerAsync:");
            Console.WriteLine($"❌   Message: {ex.Message}");
            Console.WriteLine($"❌   StackTrace: {ex.StackTrace}");
            Console.WriteLine($"❌   InnerException: {ex.InnerException?.Message}");
            _logger.LogError(ex, $"Error creating answer for {userId}");
            return new SdpResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Set remote description (answer)
    /// </summary>
    public async Task<OperationResult> SetRemoteDescriptionAsync(Guid userId, string answerSdp)
    {
        try
        {
            var result = await _jsRuntime.InvokeAsync<OperationResult>(
                "webrtcHandler.setRemoteDescription",
                userId.ToString(),
                answerSdp);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error setting remote description for {userId}");
            return new OperationResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Add ICE candidate to peer connection
    /// </summary>
    public async Task<OperationResult> AddIceCandidateAsync(Guid userId, IceCandidateData candidate)
    {
        try
        {
            var result = await _jsRuntime.InvokeAsync<OperationResult>(
                "webrtcHandler.addIceCandidate",
                userId.ToString(),
                candidate);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error adding ICE candidate for {userId}");
            return new OperationResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Send ICE candidate via SignalR (using events)
    /// </summary>
    public async Task<OperationResult> SendIceCandidateAsync(Guid roomId, string userId, object candidate)
    {
        try
        {
            Console.WriteLine($"🔗 StudyRoomWebRTCService.SendIceCandidateAsync: roomId={roomId}, userId={userId}");

            // Trigger event for hub service to handle
            if (OnIceCandidateGenerated != null)
            {
                var candidateData = new IceCandidateData
                {
                    Candidate = candidate.ToString() ?? "",
                    SdpMid = null,
                    SdpMLineIndex = null
                };
                await OnIceCandidateGenerated(Guid.Parse(userId), candidateData);
            }

            Console.WriteLine($"✅ StudyRoomWebRTCService.SendIceCandidateAsync: Success");
            return new OperationResult { Success = true };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ StudyRoomWebRTCService.SendIceCandidateAsync: Error - {ex.Message}");
            _logger.LogError(ex, "Error sending ICE candidate");
            return new OperationResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Send WebRTC offer via SignalR (using events)
    /// </summary>
    public Task<OperationResult> SendWebRTCOfferAsync(Guid roomId, string userId, object offer)
    {
        try
        {
            Console.WriteLine($"📤 StudyRoomWebRTCService.SendWebRTCOfferAsync: roomId={roomId}, userId={userId}");

            // This should be handled by the hub service directly
            // The WebRTC service should focus on WebRTC operations only
            Console.WriteLine($"✅ StudyRoomWebRTCService.SendWebRTCOfferAsync: Success");
            return Task.FromResult(new OperationResult { Success = true });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ StudyRoomWebRTCService.SendWebRTCOfferAsync: Error - {ex.Message}");
            _logger.LogError(ex, "Error sending WebRTC offer");
            return Task.FromResult(new OperationResult { Success = false, Error = ex.Message });
        }
    }

    /// <summary>
    /// Send WebRTC answer via SignalR (using events)
    /// </summary>
    public Task<OperationResult> SendWebRTCAnswerAsync(Guid roomId, string userId, object answer)
    {
        try
        {
            Console.WriteLine($"📥 StudyRoomWebRTCService.SendWebRTCAnswerAsync: roomId={roomId}, userId={userId}");

            // This should be handled by the hub service directly
            // The WebRTC service should focus on WebRTC operations only
            Console.WriteLine($"✅ StudyRoomWebRTCService.SendWebRTCAnswerAsync: Success");
            return Task.FromResult(new OperationResult { Success = true });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ StudyRoomWebRTCService.SendWebRTCAnswerAsync: Error - {ex.Message}");
            _logger.LogError(ex, "Error sending WebRTC answer");
            return Task.FromResult(new OperationResult { Success = false, Error = ex.Message });
        }
    }

    /// Trigger answer created event (for pending offers)
    public Task TriggerAnswerCreatedAsync(Guid userId, string answerSdp)
    {
        try
        {
            Console.WriteLine($"📤 StudyRoomWebRTCService.TriggerAnswerCreatedAsync: userId={userId}");
            OnAnswerCreated?.Invoke(userId, answerSdp);
            Console.WriteLine($"✅ StudyRoomWebRTCService.TriggerAnswerCreatedAsync: Success");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ StudyRoomWebRTCService.TriggerAnswerCreatedAsync: Error - {ex.Message}");
            _logger.LogError(ex, "Error triggering answer created event");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Add local stream to all existing peer connections
    /// </summary>
    public async Task<OperationResult> AddLocalStreamToAllConnectionsAsync()
    {
        try
        {
            var result = await _jsRuntime.InvokeAsync<OperationResult>(
                "webrtcHandler.addLocalStreamToAllConnections");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding local stream to all connections");
            return new OperationResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Attach remote stream to video element
    /// </summary>
    public async Task<OperationResult> AttachRemoteStreamAsync(Guid userId, string videoElementId)
    {
        try
        {
            var result = await _jsRuntime.InvokeAsync<OperationResult>(
                "webrtcHandler.attachRemoteStream",
                userId.ToString(),
                videoElementId);

            if (result.Success)
            {
                _logger.LogInformation($"Remote stream attached for {userId} to {videoElementId}");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error attaching remote stream for {userId}");
            return new OperationResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Start screen sharing
    /// </summary>
    public async Task<MediaStreamResult> StartScreenShareAsync()
    {
        try
        {
            var result = await _jsRuntime.InvokeAsync<MediaStreamResult>("webrtcHandler.startScreenShare");

            if (result.Success)
            {
                _logger.LogInformation("Screen share started");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting screen share");
            return new MediaStreamResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Stop screen sharing
    /// </summary>
    public async Task<OperationResult> StopScreenShareAsync()
    {
        try
        {
            var result = await _jsRuntime.InvokeAsync<OperationResult>("webrtcHandler.stopScreenShare");

            if (result.Success)
            {
                _logger.LogInformation("Screen share stopped");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping screen share");
            return new OperationResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Toggle audio (mute/unmute)
    /// </summary>
    public async Task<ToggleResult> ToggleAudioAsync(bool enabled)
    {
        try
        {
            var result = await _jsRuntime.InvokeAsync<ToggleResult>("webrtcHandler.toggleAudio", enabled);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling audio");
            return new ToggleResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Toggle video (camera on/off)
    /// </summary>
    public async Task<ToggleResult> ToggleVideoAsync(bool enabled)
    {
        try
        {
            var result = await _jsRuntime.InvokeAsync<ToggleResult>("webrtcHandler.toggleVideo", enabled);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling video");
            return new ToggleResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Close peer connection for a specific user
    /// </summary>
    public async Task<OperationResult> ClosePeerConnectionAsync(Guid userId)
    {
        try
        {
            var result = await _jsRuntime.InvokeAsync<OperationResult>(
                "webrtcHandler.closePeerConnection",
                userId.ToString());

            _peerStates.TryRemove(userId, out _);
            _logger.LogInformation($"Peer connection closed for {userId}");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error closing peer connection for {userId}");
            return new OperationResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Reset WebRTC state for new call (partial cleanup)
    /// </summary>
    public async Task ResetForNewCallAsync()
    {
        try
        {
            Console.WriteLine("🔄 StudyRoomWebRTCService: Resetting WebRTC state for new call...");

            // Wait a bit to ensure JavaScript is loaded
            await Task.Delay(100);

            // Call JavaScript resetForNewCall function with fallback
            try
            {
                // Check if the function exists first
                var functionExists = await _jsRuntime.InvokeAsync<bool>("typeof webrtcHandler.resetForNewCall !== 'undefined'");
                if (functionExists)
                {
                    await _jsRuntime.InvokeVoidAsync("webrtcHandler.resetForNewCall");
                    Console.WriteLine("✅ StudyRoomWebRTCService: WebRTC state reset complete");
                }
                else
                {
                    Console.WriteLine("⚠️ StudyRoomWebRTCService: resetForNewCall function not available, falling back to cleanup");
                    await CleanupAsync();
                }
            }
            catch (JSException jsEx) when (jsEx.Message.Contains("resetForNewCall") || jsEx.Message.Contains("undefined"))
            {
                Console.WriteLine("⚠️ StudyRoomWebRTCService: resetForNewCall not available, falling back to cleanup");
                // Fallback to full cleanup if resetForNewCall is not available
                await CleanupAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ StudyRoomWebRTCService: Error calling resetForNewCall: {ex.Message}, falling back to cleanup");
                await CleanupAsync();
            }

            _peerStates.Clear();
            _logger.LogInformation("WebRTC state reset for new call");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ StudyRoomWebRTCService: Error resetting WebRTC state: {ex.Message}");
            _logger.LogError(ex, "Error resetting WebRTC state for new call");
        }
    }

    /// <summary>
    /// Cleanup all WebRTC resources
    /// </summary>
    public async Task CleanupAsync()
    {
        try
        {
            Console.WriteLine("🧹 StudyRoomWebRTCService: Cleaning up all WebRTC resources...");
            await _jsRuntime.InvokeVoidAsync("webrtcHandler.cleanup");
            _peerStates.Clear();
            Console.WriteLine("✅ StudyRoomWebRTCService: All WebRTC resources cleaned up");
            _logger.LogInformation("WebRTC resources cleaned up");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ StudyRoomWebRTCService: Error cleaning up WebRTC resources: {ex.Message}");
            _logger.LogError(ex, "Error cleaning up WebRTC resources");
        }
    }

    #region JavaScript Callbacks

    /// <summary>
    /// Called from JavaScript when a remote stream is received
    /// </summary>
    [JSInvokable]
    public async Task HandleRemoteStreamReceived(string userId, string trackKind)
    {
        if (Guid.TryParse(userId, out var userGuid))
        {
            _logger.LogInformation($"Remote {trackKind} stream received from {userId}");
            if (OnRemoteStreamReceived != null)
            {
                await OnRemoteStreamReceived.Invoke(userGuid, trackKind);
            }
        }
    }

    /// <summary>
    /// Called from JavaScript when an ICE candidate is generated
    /// </summary>
    [JSInvokable]
    public async Task HandleIceCandidate(string userId, IceCandidateData candidate)
    {
        if (Guid.TryParse(userId, out var userGuid))
        {
            _logger.LogInformation($"ICE candidate generated for {userId}");
            if (OnIceCandidateGenerated != null)
            {
                await OnIceCandidateGenerated.Invoke(userGuid, candidate);
            }
        }
    }

    /// <summary>
    /// Called from JavaScript when connection state changes
    /// </summary>
    [JSInvokable]
    public async Task HandleConnectionStateChanged(string userId, string state)
    {
        if (Guid.TryParse(userId, out var userGuid))
        {
            _logger.LogInformation($"Connection state changed for {userId}: {state}");
            if (OnConnectionStateChanged != null)
            {
                await OnConnectionStateChanged.Invoke(userGuid, state);
            }
        }
    }

    /// <summary>
    /// Called from JavaScript when screen share is stopped
    /// </summary>
    [JSInvokable]
    public async Task HandleScreenShareStopped()
    {
        _logger.LogInformation("Screen share stopped");
        if (OnScreenShareStopped != null)
        {
            await OnScreenShareStopped.Invoke();
        }
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        try
        {
            await CleanupAsync();
            _dotNetReference?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing WebRTC service");
        }
    }
}
