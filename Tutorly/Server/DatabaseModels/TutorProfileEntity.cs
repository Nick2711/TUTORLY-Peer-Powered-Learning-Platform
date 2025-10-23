using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Tutorly.Server.DatabaseModels
{
    [Table("tutor_profiles")]
    public class TutorProfileEntity : BaseModel
    {
        [PrimaryKey("tutor_id", false)]
        [Column("tutor_id")]
        public int TutorId { get; set; }

        [Column("user_id")]
        public string UserId { get; set; } = string.Empty;

        [Column("programme")]
        public string Programme { get; set; } = string.Empty;

        [Column("year_of_study")]
        public int? YearOfStudy { get; set; }

        [Column("role")]
        public string Role { get; set; } = string.Empty;

        [Column("avatar_url")]
        public string? AvatarUrl { get; set; }

        [Column("full_name")]
        public string FullName { get; set; } = string.Empty;

        [Column("blurb")]
        public string Blurb { get; set; } = string.Empty;

        [Column("rating")]
        public double Rating { get; set; } = 0.0;

        [Column("status")]
        public string Status { get; set; } = "Active"; // Active, Suspended, Banned

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}


