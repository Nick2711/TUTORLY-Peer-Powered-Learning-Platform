using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System.Text.Json.Serialization;

namespace Tutorly.Server.DatabaseModels
{
    [Table("conversation_settings")]
    public class ConversationSettingsEntity : BaseModel
    {
        [PrimaryKey("setting_id", false)]
        [JsonIgnore]
        public int SettingId { get; set; }

        [Column("conversation_id")]
        public int ConversationId { get; set; }

        // Feature toggles
        [Column("allow_file_sharing")]
        public bool AllowFileSharing { get; set; } = true;

        [Column("allow_message_deletion")]
        public bool AllowMessageDeletion { get; set; } = true;

        [Column("require_approval_to_join")]
        public bool RequireApprovalToJoin { get; set; } = false;

        // Discoverability
        [Column("is_discoverable")]
        public bool IsDiscoverable { get; set; } = false;

        [Column("join_link_enabled")]
        public bool JoinLinkEnabled { get; set; } = false;

        [Column("join_link_token")]
        public string? JoinLinkToken { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}

