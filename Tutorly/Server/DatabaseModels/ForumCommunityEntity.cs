using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Tutorly.Server.DatabaseModels
{
    [Table("forum_communities")]
    public class ForumCommunityEntity : BaseModel
    {
        [PrimaryKey("community_id", false)]
        public int CommunityId { get; set; }

        [Column("community_name")]
        public string CommunityName { get; set; } = string.Empty;

        [Column("community_description")]
        public string? CommunityDescription { get; set; }

        [Column("community_type")]
        public string? CommunityType { get; set; } = "course";

        [Column("created_by_studentid")]
        public int? CreatedByStudentId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("module_id")]
        public int? ModuleId { get; set; }
    }
}
