using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Tutorly.Server.DatabaseModels;

[Table("booking_requests")]
public class BookingRequestEntity : BaseModel
{
    [PrimaryKey("request_id", false)]
    [Column("request_id")]
    public Guid RequestId { get; set; }

    [Column("student_id")]
    public int StudentId { get; set; }

    [Column("tutor_id")]
    public int TutorId { get; set; }

    [Column("module_id")]
    public int ModuleId { get; set; }

    [Column("status")]
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, Expired

    [Column("student_availability_json")]
    public string StudentAvailabilityJson { get; set; } = string.Empty;

    [Column("requested_slots_json")]
    public string RequestedSlotsJson { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("responded_at")]
    public DateTime? RespondedAt { get; set; }
}
