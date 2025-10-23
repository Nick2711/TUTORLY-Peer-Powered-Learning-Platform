using Supabase.Postgrest.Models;
using Supabase.Postgrest.Attributes;

namespace Tutorly.Server.DatabaseModels
{
    [Table("topic_subscriptions")]
    public class TopicSubscriptionEntity : BaseModel
    {
        [PrimaryKey("topic_subscriptions_id", false)]
        public int Id { get; set; }

        [Column("student_id")]
        public int StudentId { get; set; }

        [Column("topic_id")]
        public int TopicId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
