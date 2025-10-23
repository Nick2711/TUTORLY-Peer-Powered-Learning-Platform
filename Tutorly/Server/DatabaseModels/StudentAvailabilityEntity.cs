using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Tutorly.Server.DatabaseModels;

[Table("student_availability")]
public class StudentAvailabilityEntity : BaseModel
{
    [PrimaryKey("availability_id", false)]
    [Column("availability_id")]
    public Guid AvailabilityId { get; set; }

    [Column("student_id")]
    public int StudentId { get; set; }

    [Column("booking_request_id")]
    public Guid? BookingRequestId { get; set; }

    [Column("day_of_week")]
    public int DayOfWeek { get; set; } // 0-6 for Sun-Sat

    [Column("time_of_day")]
    public string TimeOfDay { get; set; } = string.Empty; // "Morning", "Afternoon", "Evening"

    [Column("specific_hours")]
    public string? SpecificHours { get; set; } // JSON array of hours

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
