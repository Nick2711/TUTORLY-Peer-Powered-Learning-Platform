using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Tutorly.Server.DatabaseModels;

[Table("tutor_availability_exceptions")]
public class TutorAvailabilityExceptionEntity : BaseModel
{
    [PrimaryKey("exception_id", false)]
    [Column("exception_id")]
    public Guid ExceptionId { get; set; }

    [Column("tutor_id")]
    public int TutorId { get; set; }

    [Column("availability_id")]
    public Guid? AvailabilityId { get; set; }

    [Column("exception_date")]
    public DateTime ExceptionDate { get; set; }

    [Column("is_available")]
    public bool IsAvailable { get; set; } // false = blackout, true = one-off available

    [Column("start_time")]
    public TimeSpan? StartTime { get; set; }

    [Column("end_time")]
    public TimeSpan? EndTime { get; set; }

    [Column("reason")]
    public string? Reason { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
