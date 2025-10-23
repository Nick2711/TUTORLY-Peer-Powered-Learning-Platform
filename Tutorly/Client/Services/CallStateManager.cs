using System;
using System.Threading.Tasks;

namespace Tutorly.Client.Services
{
    /// <summary>
    /// Manages call state to prevent overlapping calls
    /// </summary>
    public class CallStateManager
    {
        private static CallStateManager? _instance;
        private static readonly object _lock = new object();

        public static CallStateManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new CallStateManager();
                        }
                    }
                }
                return _instance;
            }
        }

        private CallStateManager() { }

        public CallState CurrentState { get; private set; } = CallState.Idle;
        public Guid? CurrentCallId { get; private set; }
        public DateTime? CallStartTime { get; private set; }

        /// <summary>
        /// Check if a new call can be initiated
        /// </summary>
        public bool CanInitiateCall()
        {
            return CurrentState == CallState.Idle;
        }

        /// <summary>
        /// Check if a call can be accepted
        /// </summary>
        public bool CanAcceptCall()
        {
            return CurrentState == CallState.Idle || CurrentState == CallState.Incoming;
        }

        /// <summary>
        /// Start initiating a call
        /// </summary>
        public bool StartInitiatingCall(Guid callId)
        {
            lock (_lock)
            {
                if (CurrentState != CallState.Idle)
                {
                    Console.WriteLine($"❌ CallStateManager: Cannot start call {callId} - current state: {CurrentState}");
                    return false;
                }

                CurrentState = CallState.Initiating;
                CurrentCallId = callId;
                CallStartTime = DateTime.UtcNow;
                Console.WriteLine($"📞 CallStateManager: Started initiating call {callId}");
                return true;
            }
        }

        /// <summary>
        /// Start receiving an incoming call
        /// </summary>
        public bool StartIncomingCall(Guid callId)
        {
            lock (_lock)
            {
                if (CurrentState != CallState.Idle)
                {
                    Console.WriteLine($"❌ CallStateManager: Cannot receive call {callId} - current state: {CurrentState}");
                    return false;
                }

                CurrentState = CallState.Incoming;
                CurrentCallId = callId;
                CallStartTime = DateTime.UtcNow;
                Console.WriteLine($"📞 CallStateManager: Started receiving call {callId}");
                return true;
            }
        }

        /// <summary>
        /// Complete call initiation (call sent)
        /// </summary>
        public void CompleteInitiatingCall()
        {
            lock (_lock)
            {
                if (CurrentState == CallState.Initiating)
                {
                    CurrentState = CallState.WaitingForResponse;
                    Console.WriteLine($"📞 CallStateManager: Call {CurrentCallId} initiated, waiting for response");
                }
            }
        }

        /// <summary>
        /// Accept a call
        /// </summary>
        public bool AcceptCall()
        {
            lock (_lock)
            {
                if (CurrentState == CallState.Incoming)
                {
                    CurrentState = CallState.Accepted;
                    Console.WriteLine($"📞 CallStateManager: Call {CurrentCallId} accepted");
                    return true;
                }
                else
                {
                    Console.WriteLine($"❌ CallStateManager: Cannot accept call - current state: {CurrentState}");
                    return false;
                }
            }
        }

        /// <summary>
        /// Reject a call
        /// </summary>
        public void RejectCall()
        {
            lock (_lock)
            {
                if (CurrentState == CallState.Incoming)
                {
                    CurrentState = CallState.Rejected;
                    Console.WriteLine($"📞 CallStateManager: Call {CurrentCallId} rejected");
                }
            }
        }

        /// <summary>
        /// Start an active call
        /// </summary>
        public void StartActiveCall()
        {
            lock (_lock)
            {
                if (CurrentState == CallState.Accepted || CurrentState == CallState.WaitingForResponse)
                {
                    CurrentState = CallState.Active;
                    Console.WriteLine($"📞 CallStateManager: Call {CurrentCallId} is now active");
                }
            }
        }

        /// <summary>
        /// End the current call
        /// </summary>
        public void EndCall()
        {
            lock (_lock)
            {
                var callId = CurrentCallId;
                var duration = CallStartTime.HasValue ? DateTime.UtcNow - CallStartTime.Value : TimeSpan.Zero;

                CurrentState = CallState.Idle;
                CurrentCallId = null;
                CallStartTime = null;

                Console.WriteLine($"📞 CallStateManager: Call {callId} ended (duration: {duration.TotalSeconds:F1}s)");
            }
        }

        /// <summary>
        /// Force reset to idle state (emergency cleanup)
        /// </summary>
        public void ForceReset()
        {
            lock (_lock)
            {
                var callId = CurrentCallId;
                CurrentState = CallState.Idle;
                CurrentCallId = null;
                CallStartTime = null;
                Console.WriteLine($"🔄 CallStateManager: Force reset to idle (was: {callId})");
            }
        }

        /// <summary>
        /// Get current call duration
        /// </summary>
        public TimeSpan? GetCallDuration()
        {
            if (CallStartTime.HasValue && CurrentState == CallState.Active)
            {
                return DateTime.UtcNow - CallStartTime.Value;
            }
            return null;
        }
    }

    public enum CallState
    {
        Idle,
        Initiating,
        Incoming,
        WaitingForResponse,
        Accepted,
        Active,
        Rejected
    }
}
