using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Tutorly.Server.DatabaseModels
{
    [Table("user_warnings")]
    public class UserWarningEntity : BaseModel
    {
        [PrimaryKey("warning_id", false)]
        public int WarningId { get; set; }

        [Column("user_id")]
        public string UserId { get; set; } = string.Empty;

        [Column("warned_by_admin_id")]
        public string WarnedByAdminId { get; set; } = string.Empty;

        [Column("warning_message")]
        public string WarningMessage { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Computed properties for display (not mapped to database)
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        [Newtonsoft.Json.JsonIgnore]
        public string? AdminName { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        [Newtonsoft.Json.JsonIgnore]
        public string? UserName { get; set; }
    }
}