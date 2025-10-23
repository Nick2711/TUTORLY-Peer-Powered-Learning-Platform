using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Tutorly.Server.DatabaseModels
{
    [Table("forum_threads")]
    public class ForumThreadEntity : BaseModel
    {
        [PrimaryKey("thread_id", false)]
        public int ThreadId { get; set; }

        [Column("community_id")]
        public int CommunityId { get; set; }

        [Column("thread_name")]
        public string ThreadName { get; set; } = string.Empty;

        [Column("thread_description")]
        public string? ThreadDescription { get; set; }

        [Column("created_by_studentid")]
        public int? CreatedByStudentId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;
    }
}
