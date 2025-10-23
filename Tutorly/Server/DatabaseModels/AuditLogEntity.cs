using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Tutorly.Server.DatabaseModels
{
    [Table("audit_logs")]
    public class AuditLogEntity : BaseModel
    {
        [PrimaryKey("log_id", false)]
        public Guid LogId { get; set; }

        [Column("event_type")]
        public string EventType { get; set; } = string.Empty;

        [Column("user_id")]
        public Guid? UserId { get; set; }

        [Column("entity_type")]
        public string? EntityType { get; set; }

        [Column("entity_id")]
        public string? EntityId { get; set; }

        [Column("details")]
        public string? Details { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}

