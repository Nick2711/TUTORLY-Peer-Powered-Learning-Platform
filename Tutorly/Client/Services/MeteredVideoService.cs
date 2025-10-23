using Microsoft.JSInterop;

namespace Tutorly.Client.Services;

public class MeteredVideoService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<MeteredVideoService> _logger;
    private IJSObjectReference? _meeting;
    private bool _isInitialized = false;

    public event Func<string, Task>? OnParticipantJoined;
    public event Func<string, Task>? OnParticipantLeft;
    public event Func<string, Task>? OnLocalTrackStarted;
    public event Func<string, Task>? OnRemoteTrackStarted;

    public MeteredVideoService(IJSRuntime jsRuntime, ILogger<MeteredVideoService> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Initializing Metered Video Service");

            // Wait for Metered SDK to be loaded
            await _jsRuntime.InvokeAsync<bool>("meteredVideoInterop.waitForSDK");

            _isInitialized = true;
            _logger.LogInformation("Metered Video Service initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing Metered Video Service");
            throw;
        }
    }

    public async Task<bool> JoinRoomAsync(string roomUrl, string participantName, string? accessToken = null)
    {
        try
        {
            if (!_isInitialized)
            {
                await InitializeAsync();
            }

            _logger.LogInformation($"Joining Metered room: {roomUrl}");

            // Create meeting object using Metered SDK
            _meeting = await _jsRuntime.InvokeAsync<IJSObjectReference>("meteredVideoInterop.createMeeting");

            // Set up event handlers
            await SetupEventHandlers();

            // Join the room
            var joinOptions = new { roomURL = roomUrl, name = participantName, token = accessToken };
            var meetingInfo = await _jsRuntime.InvokeAsync<object>("meteredVideoInterop.joinMeeting", _meeting, joinOptions);

            _logger.LogInformation("Successfully joined Metered room");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error joining Metered room: {ex.Message}");
            return false;
        }
    }

    public async Task StartVideoAsync()
    {
        try
        {
            if (_meeting == null)
            {
                throw new InvalidOperationException("Not connected to a room");
            }

            await _jsRuntime.InvokeVoidAsync("meteredVideoInterop.startVideo", _meeting);
            _logger.LogInformation("Started video");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error starting video: {ex.Message}");
            throw;
        }
    }

    public async Task StopVideoAsync()
    {
        try
        {
            if (_meeting == null)
            {
                throw new InvalidOperationException("Not connected to a room");
            }

            await _jsRuntime.InvokeVoidAsync("stopVideo", _meeting);
            _logger.LogInformation("Stopped video");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error stopping video: {ex.Message}");
            throw;
        }
    }

    public async Task StartAudioAsync()
    {
        try
        {
            if (_meeting == null)
            {
                throw new InvalidOperationException("Not connected to a room");
            }

            await _jsRuntime.InvokeVoidAsync("startAudio", _meeting);
            _logger.LogInformation("Started audio");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error starting audio: {ex.Message}");
            throw;
        }
    }

    public async Task StopAudioAsync()
    {
        try
        {
            if (_meeting == null)
            {
                throw new InvalidOperationException("Not connected to a room");
            }

            await _jsRuntime.InvokeVoidAsync("stopAudio", _meeting);
            _logger.LogInformation("Stopped audio");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error stopping audio: {ex.Message}");
            throw;
        }
    }

    public async Task LeaveRoomAsync()
    {
        try
        {
            if (_meeting == null)
            {
                return;
            }

            await _jsRuntime.InvokeVoidAsync("leaveMeeting", _meeting);
            _meeting = null;
            _logger.LogInformation("Left Metered room");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error leaving room: {ex.Message}");
        }
    }

    private async Task SetupEventHandlers()
    {
        if (_meeting == null) return;

        // Set up JavaScript event handlers
        await _jsRuntime.InvokeVoidAsync("meteredVideoInterop.setupMeteredEventHandlers", _meeting, DotNetObjectReference.Create(this));
    }

    [JSInvokable]
    public async Task OnParticipantJoinedJS(string participantInfo)
    {
        try
        {
            _logger.LogInformation($"Participant joined: {participantInfo}");
            if (OnParticipantJoined != null)
            {
                await OnParticipantJoined.Invoke(participantInfo);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling participant joined event");
        }
    }

    [JSInvokable]
    public async Task OnParticipantLeftJS(string participantInfo)
    {
        try
        {
            _logger.LogInformation($"Participant left: {participantInfo}");
            if (OnParticipantLeft != null)
            {
                await OnParticipantLeft.Invoke(participantInfo);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling participant left event");
        }
    }

    [JSInvokable]
    public async Task OnLocalTrackStartedJS(string trackInfo)
    {
        try
        {
            _logger.LogInformation($"Local track started: {trackInfo}");
            if (OnLocalTrackStarted != null)
            {
                await OnLocalTrackStarted.Invoke(trackInfo);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling local track started event");
        }
    }

    [JSInvokable]
    public async Task OnRemoteTrackStartedJS(string trackInfo)
    {
        try
        {
            _logger.LogInformation($"Remote track started: {trackInfo}");
            if (OnRemoteTrackStarted != null)
            {
                await OnRemoteTrackStarted.Invoke(trackInfo);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling remote track started event");
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await LeaveRoomAsync();
            if (_meeting != null)
            {
                await _meeting.DisposeAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing Metered Video Service");
        }
    }
}
