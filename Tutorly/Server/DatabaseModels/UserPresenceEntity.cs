using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Tutorly.Server.DatabaseModels
{
    [Table("user_presence")]
    public class UserPresenceEntity : BaseModel
    {
        [PrimaryKey("user_id")]
        [Column("user_id")]
        public string UserId { get; set; } = string.Empty;

        [Column("status")]
        public string Status { get; set; } = "offline"; // online, away, offline

        [Column("last_seen_at")]
        public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}