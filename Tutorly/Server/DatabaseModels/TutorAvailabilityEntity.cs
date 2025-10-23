using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Tutorly.Server.DatabaseModels;

[Table("tutor_availability")]
public class TutorAvailabilityEntity : BaseModel
{
    [PrimaryKey("availability_id", false)]
    [Column("availability_id")]
    public Guid AvailabilityId { get; set; }

    [Column("tutor_id")]
    public int TutorId { get; set; }

    [Column("module_id")]
    public int? ModuleId { get; set; }

    [Column("day_of_week")]
    public int DayOfWeek { get; set; } // 0-6 for Sun-Sat

    [Column("start_time")]
    public TimeSpan StartTime { get; set; }

    [Column("end_time")]
    public TimeSpan EndTime { get; set; }

    [Column("is_recurring")]
    public bool IsRecurring { get; set; } = true;

    [Column("effective_from")]
    public DateTime EffectiveFrom { get; set; }

    [Column("effective_until")]
    public DateTime? EffectiveUntil { get; set; }

    [Column("timezone")]
    public string Timezone { get; set; } = "Africa/Johannesburg";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
