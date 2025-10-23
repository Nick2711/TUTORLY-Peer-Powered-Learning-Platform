using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System.Text.Json.Serialization;

namespace Tutorly.Server.DatabaseModels
{
    [Table("conversations")]
    public class ConversationEntity : BaseModel
    {
        [PrimaryKey("conversation_id", false)]
        [JsonIgnore]
        public int ConversationId { get; set; }

        [Column("conversation_type")]
        public string ConversationType { get; set; } = "direct"; // direct, group

        // Group-specific properties
        [Column("group_name")]
        public string? GroupName { get; set; }

        [Column("group_description")]
        public string? GroupDescription { get; set; }

        [Column("group_avatar_url")]
        public string? GroupAvatarUrl { get; set; }

        [Column("created_by_user_id")]
        public string CreatedByUserId { get; set; } = string.Empty;

        [Column("max_participants")]
        public int MaxParticipants { get; set; } = 50;

        // Timestamps
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("last_message_at")]
        public DateTime? LastMessageAt { get; set; }

        // Status
        [Column("is_active")]
        public bool IsActive { get; set; } = true;
    }
}
