using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Tutorly.Server.DatabaseModels;

[Table("module_tutor_preferences")]
public class ModuleTutorPreferencesEntity : BaseModel
{
    [PrimaryKey("preference_id", false)]
    [Column("preference_id")]
    public Guid PreferenceId { get; set; }

    [Column("tutor_id")]
    public int TutorId { get; set; }

    [Column("module_id")]
    public int ModuleId { get; set; }

    [Column("slot_length_minutes")]
    public int SlotLengthMinutes { get; set; } = 60;

    [Column("buffer_minutes")]
    public int BufferMinutes { get; set; } = 10;

    [Column("lead_time_hours")]
    public int LeadTimeHours { get; set; } = 4;

    [Column("booking_window_days")]
    public int BookingWindowDays { get; set; } = 21;

    [Column("max_sessions_per_day")]
    public int MaxSessionsPerDay { get; set; } = 5;

    [Column("cancellation_cutoff_hours")]
    public int CancellationCutoffHours { get; set; } = 12;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
