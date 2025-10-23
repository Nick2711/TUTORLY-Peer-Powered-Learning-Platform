namespace Tutorly.Shared
{
    /// <summary>
    /// Error codes for booking-related operations
    /// </summary>
    public static class BookingErrorCodes
    {
        public const string SLOT_NO_LONGER_AVAILABLE = "SLOT_UNAVAILABLE";
        public const string SLOT_LOCKED = "SLOT_LOCKED";
        public const string MINIMUM_ADVANCE_NOT_MET = "ADVANCE_BOOKING_REQUIRED";
        public const string DAILY_LIMIT_REACHED = "DAILY_LIMIT";
        public const string BOOKING_WINDOW_EXCEEDED = "BOOKING_WINDOW";
        public const string BUFFER_CONFLICT = "BUFFER_CONFLICT";
        public const string STUDENT_PREFERENCE_MISMATCH = "STUDENT_PREFERENCE";
        public const string LEAD_TIME_NOT_MET = "LEAD_TIME";
    }
}

