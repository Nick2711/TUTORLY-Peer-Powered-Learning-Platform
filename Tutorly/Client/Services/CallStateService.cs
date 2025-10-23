using Tutorly.Shared;

namespace Tutorly.Client.Services;

public class CallStateService
{
    private CallStateDto? _activeCall;
    public event Action? OnActiveCallChanged;

    public CallStateDto? ActiveCall
    {
        get => _activeCall;
        set
        {
            _activeCall = value;
            OnActiveCallChanged?.Invoke();
        }
    }

    public bool HasActiveCall => _activeCall != null;

    public void StartCall(CallStateDto callState)
    {
        ActiveCall = callState;
    }

    public void EndCall()
    {
        ActiveCall = null;
    }

    public void UpdateCallState(CallStateDto callState)
    {
        if (_activeCall != null && _activeCall.RoomId == callState.RoomId)
        {
            ActiveCall = callState;
        }
    }
}
