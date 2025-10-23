using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System.Text.Json.Serialization;

namespace Tutorly.Server.DatabaseModels
{
    [Table("tutor_applications")]
    public class TutorApplicationEntity : BaseModel
    {
        [PrimaryKey("application_id", false)]
        [JsonIgnore]
        public int ApplicationId { get; set; }

        [Column("user_id")]
        public string UserId { get; set; } = string.Empty;

        [Column("full_name")]
        public string FullName { get; set; } = string.Empty;

        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Column("phone")]
        public string? Phone { get; set; }

        [Column("programme")]
        public string Programme { get; set; } = string.Empty;

        [Column("year_of_study")]
        public int YearOfStudy { get; set; }

        [Column("gpa")]
        public decimal? GPA { get; set; }

        [Column("transcript_url")]
        public string? TranscriptUrl { get; set; }

        [Column("transcript_filename")]
        public string? TranscriptFilename { get; set; }

        [Column("motivation")]
        public string Motivation { get; set; } = string.Empty;

        [Column("previous_experience")]
        public string? PreviousExperience { get; set; }

        [Column("subjects_interested")]
        public string SubjectsInterested { get; set; } = string.Empty;

        [Column("availability")]
        public string? Availability { get; set; }

        [Column("status")]
        public string Status { get; set; } = "pending"; // pending, approved, rejected

        [Column("admin_notes")]
        public string? AdminNotes { get; set; }

        [Column("reviewed_by")]
        public string? ReviewedBy { get; set; }

        [Column("reviewed_at")]
        public DateTime? ReviewedAt { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
