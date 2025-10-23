using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Tutorly.Server.DatabaseModels
{
    [Table("student_profiles")]
    public class StudentProfileEntity : BaseModel
    {
        [PrimaryKey("student_id")]
        [Column("student_id")]
        public int StudentId { get; set; }

        [Column("user_id")]
        public string UserId { get; set; } = string.Empty;

        [Column("programme")]
        public string Programme { get; set; } = string.Empty;

        [Column("year_of_study")]
        public int? YearOfStudy { get; set; }

        [Column("role")]
        public string Role { get; set; } = "student";

        [Column("avatar_url")]
        public string? AvatarUrl { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("full_name")]
        public string FullName { get; set; } = string.Empty;

        [Column("status")]
        public string Status { get; set; } = "Active"; // Active, Suspended, Banned
    }
}
