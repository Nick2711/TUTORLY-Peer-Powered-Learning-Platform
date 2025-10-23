using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Tutorly.Server.DatabaseModels
{
    [Table("forum_posts")]
    public class ForumPostEntity : BaseModel
    {
        [PrimaryKey("forum_posts_id", false)]
        public int ForumPostsId { get; set; }

        [Column("created_by_studentid")]
        public int? CreatedByStudentId { get; set; }

        [Column("title")]
        public string Title { get; set; } = string.Empty;

        [Column("content")]
        public string Content { get; set; } = string.Empty;

        [Column("is_anonymous")]
        public bool IsAnonymous { get; set; } = false;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("deleted_at")]
        public DateTime? DeletedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("module_id")]
        public int? ModuleId { get; set; }

        [Column("post_type")]
        public string PostType { get; set; } = "question";

        [Column("tag")]
        public string? Tag { get; set; }

        [Column("thread_id")]
        public int? ThreadId { get; set; }
    }
}

