using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Tutorly.Server.DatabaseModels
{
    [Table("forum_votes")]
    public class ForumVoteEntity : BaseModel
    {
        [PrimaryKey("forum_votes_id", false)]
        public int ForumVotesId { get; set; }

        [Column("forum_responses_id")]
        public int? ForumResponsesId { get; set; } // Nullable to support post votes

        [Column("forum_posts_id")]
        public int? ForumPostsId { get; set; } // Added to support post votes

        [Column("student_id")]
        public int StudentId { get; set; }

        [Column("vote_type")]
        public int VoteType { get; set; } // 1 for upvote, -1 for downvote

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

