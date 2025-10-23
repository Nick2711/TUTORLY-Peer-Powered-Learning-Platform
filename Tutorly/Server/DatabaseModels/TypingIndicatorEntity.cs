using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System.Text.Json.Serialization;

namespace Tutorly.Server.DatabaseModels
{
    [Table("typing_indicators")]
    public class TypingIndicatorEntity : BaseModel
    {
        [PrimaryKey("typing_id", false)]
        [JsonIgnore]
        public int TypingId { get; set; }

        [Column("conversation_id")]
        public int ConversationId { get; set; }

        [Column("user_id")]
        public string UserId { get; set; } = string.Empty;

        [Column("started_at")]
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        [Column("expires_at")]
        public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddSeconds(10);
    }
}

