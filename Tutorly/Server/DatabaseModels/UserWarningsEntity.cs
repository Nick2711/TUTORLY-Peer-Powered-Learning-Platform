using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Tutorly.Server.DatabaseModels
{
    [Table("user_warnings")]
    public class UserWarningsEntity : BaseModel
    {
        [PrimaryKey("warning_id")]
        public int WarningId { get; set; }

        [Column("user_id")]
        public string UserId { get; set; } = string.Empty;

        [Column("warned_by_admin_id")]
        public string WarnedByAdminId { get; set; } = string.Empty;

        [Column("warning_message")]
        public string WarningMessage { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
