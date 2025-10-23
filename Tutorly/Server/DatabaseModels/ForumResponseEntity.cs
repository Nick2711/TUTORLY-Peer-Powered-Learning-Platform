using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Tutorly.Server.DatabaseModels
{
    [Table("forum_responses")]
    public class ForumResponseEntity : BaseModel
    {
        [PrimaryKey("forum_responses_id", false)]
        public int ForumResponsesId { get; set; }

        [Column("forum_posts_id")]
        public int ForumPostsId { get; set; }

        // Student author (nullable to support tutor-authored responses)
        [Column("created_by_studentid")]
        public int? CreatedByStudentId { get; set; }

        // New: Tutor author (nullable)
        [Column("created_by_tutorid")]
        public int? CreatedByTutorId { get; set; }

        [Column("materials_id")]
        public int? MaterialsId { get; set; }

        [Column("content")]
        public string Content { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("is_tutor_verified")]
        public bool IsTutorVerified { get; set; } = false;

        [Column("verified_by_tutorid")]
        public int? VerifiedByTutorId { get; set; }

        [Column("verified_at")]
        public DateTime? VerifiedAt { get; set; }
    }
}

