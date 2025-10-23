namespace Tutorly.Shared;

public class AvailabilityBlockDto
{
    public Guid? AvailabilityId { get; set; }
    public int TutorId { get; set; }
    public int? ModuleId { get; set; }
    public int DayOfWeek { get; set; } // 0-6
    public string StartTime { get; set; } = string.Empty; // "09:00"
    public string EndTime { get; set; } = string.Empty;   // "17:00"
    public bool IsRecurring { get; set; } = true;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveUntil { get; set; }
    public string Timezone { get; set; } = "Africa/Johannesburg";
}

public class AvailabilityExceptionDto
{
    public Guid? ExceptionId { get; set; }
    public int TutorId { get; set; }
    public Guid? AvailabilityId { get; set; }
    public DateTime ExceptionDate { get; set; }
    public bool IsAvailable { get; set; } // false=blackout
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string? Reason { get; set; }

    // Helper properties for UI binding
    public string StartTimeString { get; set; } = "09:00";
    public string EndTimeString { get; set; } = "10:00";
}

public class StudentAvailabilityDto
{
    public List<int> PreferredDays { get; set; } = new(); // [1,2,3] = Mon,Tue,Wed
    public List<string> PreferredTimes { get; set; } = new(); // ["Morning","Evening"]
    public Dictionary<int, List<int>>? SpecificHours { get; set; } // day -> hours
}

public class BookableSlotDto
{
    public DateTime SlotStart { get; set; }
    public DateTime SlotEnd { get; set; }
    public bool IsAvailable { get; set; }
    public string? UnavailableReason { get; set; } // "Lead time", "Buffer conflict", "Daily cap", etc.
    public string DisplayTime { get; set; } = string.Empty; // "Mon, Oct 20 - 10:00 AM"
}

public class BookingRequestDto
{
    public Guid? RequestId { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty; // Student name for display
    public int TutorId { get; set; }
    public int ModuleId { get; set; }
    public string ModuleName { get; set; } = string.Empty; // Module name for display
    public List<DateTime> RequestedSlots { get; set; } = new();
    public string StudentPreferencesJson { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RespondedAt { get; set; }
}

public class SessionDto
{
    public Guid SessionId { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public int TutorId { get; set; }
    public string TutorName { get; set; } = string.Empty;
    public int ModuleId { get; set; }
    public string ModuleCode { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public DateTime ScheduledStart { get; set; }
    public DateTime ScheduledEnd { get; set; }
    public Guid? StudyRoomId { get; set; }
    public string Status { get; set; } = "Pending";
    public string? CancellationReason { get; set; }
    public DateTime? CancelledAt { get; set; }
}

public class CalendarEventDto
{
    public Guid SessionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public string Type { get; set; } = "TutorSession";
    public Guid? StudyRoomId { get; set; }
    public string? ParticipantName { get; set; }
    public string Status { get; set; } = "Pending";
}

public class ModuleTutorPreferencesDto
{
    public Guid? PreferenceId { get; set; }
    public int TutorId { get; set; }
    public int ModuleId { get; set; }
    public int SlotLengthMinutes { get; set; } = 60;
    public int BufferMinutes { get; set; } = 10;
    public int LeadTimeHours { get; set; } = 4;
    public int BookingWindowDays { get; set; } = 21;
    public int MaxSessionsPerDay { get; set; } = 5;
    public int CancellationCutoffHours { get; set; } = 12;
}

public class UpcomingSessionDto
{
    public Guid SessionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string ModuleCode { get; set; } = string.Empty;
    public string ParticipantName { get; set; } = string.Empty;
    public DateTime ScheduledStart { get; set; }
    public DateTime ScheduledEnd { get; set; }
    public string Status { get; set; } = "Confirmed";
    public Guid? StudyRoomId { get; set; }
    public string TimeDisplay { get; set; } = string.Empty;
    public string DateDisplay { get; set; } = string.Empty;
    public bool IsToday { get; set; }
    public bool IsTomorrow { get; set; }

    // Action button states
    public bool CanJoin { get; set; } // True if session is within 15 min of start
    public bool CanReschedule { get; set; } // True if more than 24 hours away
    public bool CanCancel { get; set; } // True if more than cancellation cutoff
    public string StatusColor { get; set; } = "default"; // For UI styling
}

public class CreateSessionRequest
{
    public int TutorId { get; set; }
    public int StudentId { get; set; }
    public int ModuleId { get; set; }
    public DateTime ScheduledStart { get; set; }
    public int DurationMinutes { get; set; } = 60;
    public string Platform { get; set; } = "Tutorly"; // For future use (in-person vs tutorly)
}

public class TutorAnalyticsDto
{
    public int TotalSessions { get; set; }
    public int UniqueStudents { get; set; }
    public double TotalHours { get; set; }
    public double AverageRating { get; set; }
    public double NoShowRate { get; set; }
    public int VerifiedResponses { get; set; }
    public int DaysAnalyzed { get; set; }
    public List<TutorAnalyticsSessionDto> RecentSessions { get; set; } = new();
    public List<TutorAnalyticsStudentDto> TopStudents { get; set; } = new();
}

public class TutorAnalyticsSessionDto
{
    public Guid SessionId { get; set; }
    public DateTime ScheduledStart { get; set; }
    public DateTime ScheduledEnd { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public double? Rating { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsNoShow { get; set; }
}

public class TutorAnalyticsStudentDto
{
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public int SessionCount { get; set; }
    public double TotalHours { get; set; }
    public double? AverageRating { get; set; }
}