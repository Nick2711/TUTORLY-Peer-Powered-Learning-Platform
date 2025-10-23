using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System.Text.Json.Serialization;


namespace Tutorly.Server.DatabaseModels
{
    [Table("forum_community_memberships")]
    public class ForumCommunityMembershipEntity : BaseModel
    {
        [PrimaryKey("membership_id", false)]
        [JsonIgnore]
        public int MembershipId { get; set; }

        [Column("community_id")]
        public int? CommunityId { get; set; }

        [Column("student_id")]
        public int? StudentId { get; set; }

        [Column("tutor_id")]
        public int? TutorId { get; set; }

        [Column("joined_at")]
        public DateTime? JoinedAt { get; set; }
    }
}

