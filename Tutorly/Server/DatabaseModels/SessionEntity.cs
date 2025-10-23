using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Tutorly.Server.DatabaseModels;

[Table("sessions")]
public class SessionEntity : BaseModel
{
    [PrimaryKey("session_id", false)]
    [Column("session_id")]
    public Guid SessionId { get; set; }

    [Column("booking_request_id")]
    public Guid? BookingRequestId { get; set; }

    [Column("student_id")]
    public int StudentId { get; set; }

    [Column("tutor_id")]
    public int TutorId { get; set; }

    [Column("module_id")]
    public int ModuleId { get; set; }

    [Column("study_room_id")]
    public Guid? StudyRoomId { get; set; }

    [Column("scheduled_start")]
    public DateTime ScheduledStart { get; set; } // UTC

    [Column("scheduled_end")]
    public DateTime ScheduledEnd { get; set; } // UTC

    [Column("status")]
    public string Status { get; set; } = "Pending"; // Pending, Confirmed, InProgress, Completed, Cancelled

    [Column("cancellation_reason")]
    public string? CancellationReason { get; set; }

    [Column("cancelled_by")]
    public int? CancelledBy { get; set; }

    [Column("cancelled_at")]
    public DateTime? CancelledAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
