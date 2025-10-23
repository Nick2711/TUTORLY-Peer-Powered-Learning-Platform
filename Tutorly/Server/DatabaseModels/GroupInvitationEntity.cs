using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System.Text.Json.Serialization;

namespace Tutorly.Server.DatabaseModels
{
    [Table("group_invitations")]
    public class GroupInvitationEntity : BaseModel
    {
        [PrimaryKey("invitation_id", false)]
        [JsonIgnore]
        public int InvitationId { get; set; }

        [Column("conversation_id")]
        public int ConversationId { get; set; }

        [Column("invited_user_id")]
        public string InvitedUserId { get; set; } = string.Empty;

        [Column("invited_by_user_id")]
        public string InvitedByUserId { get; set; } = string.Empty;

        [Column("status")]
        public string Status { get; set; } = "pending"; // pending, accepted, declined

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("responded_at")]
        public DateTime? RespondedAt { get; set; }
    }
}

