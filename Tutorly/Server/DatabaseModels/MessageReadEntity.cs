using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System.Text.Json.Serialization;

namespace Tutorly.Server.DatabaseModels
{
    [Table("message_reads")]
    public class MessageReadEntity : BaseModel
    {
        [PrimaryKey("read_id", false)]
        [JsonIgnore]
        public int ReadId { get; set; }

        [Column("message_id")]
        public int MessageId { get; set; }

        [Column("user_id")]
        public string UserId { get; set; } = string.Empty;

        [Column("read_at")]
        public DateTime ReadAt { get; set; } = DateTime.UtcNow;
    }
}