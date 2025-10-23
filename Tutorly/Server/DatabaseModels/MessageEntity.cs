using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System.Text.Json.Serialization;

namespace Tutorly.Server.DatabaseModels
{
    [Table("messages")]
    public class MessageEntity : BaseModel
    {
        [PrimaryKey("message_id", false)]
        [JsonIgnore]
        public int MessageId { get; set; }

        [Column("conversation_id")]
        public int ConversationId { get; set; }

        [Column("sender_user_id")]
        public string SenderUserId { get; set; } = string.Empty;

        // Message content
        [Column("content")]
        public string Content { get; set; } = string.Empty;

        [Column("message_type")]
        public string MessageType { get; set; } = "text"; // text, file, image, system, announcement

        // File attachments
        [Column("file_url")]
        public string? FileUrl { get; set; }

        [Column("file_name")]
        public string? FileName { get; set; }

        [Column("file_size")]
        public long? FileSize { get; set; }

        // Message metadata
        [Column("is_edited")]
        public bool IsEdited { get; set; } = false;

        [Column("deleted_at")]
        public DateTime? DeletedAt { get; set; }

        // Reply/threading
        [Column("reply_to_message_id")]
        public int? ReplyToMessageId { get; set; }

        // Pinned messages (for groups)
        [Column("is_pinned")]
        public bool IsPinned { get; set; } = false;

        [Column("pinned_at")]
        public DateTime? PinnedAt { get; set; }

        [Column("pinned_by_user_id")]
        public string? PinnedByUserId { get; set; }

        // Timestamps
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}
