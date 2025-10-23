using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Tutorly.Server.DatabaseModels
{
    [Table("user_bans")]
    public class UserBanEntity : BaseModel
    {
        [PrimaryKey("ban_id", false)]
        public int BanId { get; set; }

        [Column("user_id")]
        public string UserId { get; set; } = string.Empty;

        [Column("banned_by_admin_id")]
        public string BannedByAdminId { get; set; } = string.Empty;

        [Column("ban_reason")]
        public string BanReason { get; set; } = string.Empty;

        [Column("ban_type")]
        public string BanType { get; set; } = "temporary"; // temporary, permanent, auto

        [Column("banned_at")]
        public DateTime BannedAt { get; set; } = DateTime.UtcNow;

        [Column("expires_at")]
        public DateTime? ExpiresAt { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("unbanned_at")]
        public DateTime? UnbannedAt { get; set; }

        [Column("unbanned_by_admin_id")]
        public string? UnbannedByAdminId { get; set; }

        [Column("unban_reason")]
        public string? UnbanReason { get; set; }

        // Computed properties for display (not mapped to database)
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        [Newtonsoft.Json.JsonIgnore]
        public string? AdminName { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        [Newtonsoft.Json.JsonIgnore]
        public string? UserName { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        [Newtonsoft.Json.JsonIgnore]
        public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;
    }
}
