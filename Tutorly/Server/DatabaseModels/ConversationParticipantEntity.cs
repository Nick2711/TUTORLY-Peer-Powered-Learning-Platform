using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System.Text.Json.Serialization;

namespace Tutorly.Server.DatabaseModels
{
    [Table("conversation_participants")]
    public class ConversationParticipantEntity : BaseModel
    {
        [PrimaryKey("participant_id", false)]
        [JsonIgnore]
        public int ParticipantId { get; set; }

        [Column("conversation_id")]
        public int ConversationId { get; set; }

        [Column("user_id")]
        public string UserId { get; set; } = string.Empty;

        // Role and permissions
        [Column("role")]
        public string Role { get; set; } = "member"; // owner, admin, member

        [Column("can_add_members")]
        public bool CanAddMembers { get; set; } = false;

        [Column("can_remove_members")]
        public bool CanRemoveMembers { get; set; } = false;

        // User preferences
        [Column("nickname")]
        public string? Nickname { get; set; }

        [Column("is_muted")]
        public bool IsMuted { get; set; } = false;

        // Timestamps
        [Column("joined_at")]
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        [Column("left_at")]
        public DateTime? LeftAt { get; set; }

        [Column("last_read_at")]
        public DateTime? LastReadAt { get; set; }
    }
}
