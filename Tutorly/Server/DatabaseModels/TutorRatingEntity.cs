using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Tutorly.Server.DatabaseModels
{
    [Table("tutor_ratings")]
    public class TutorRatingEntity : BaseModel
    {
        [PrimaryKey("rating_id", false)]
        public Guid RatingId { get; set; }

        [Column("tutor_id")]
        public int TutorId { get; set; }

        [Column("student_id")]
        public int StudentId { get; set; }

        [Column("module_id")]
        public int ModuleId { get; set; }

        [Column("rating")]
        public int Rating { get; set; } // 1-5 stars

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }
}
