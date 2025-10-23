using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Tutorly.Server.DatabaseModels
{
    [Table("admins")]
    public class AdminProfileEntity : BaseModel
    {
        [PrimaryKey("admin_id")]
        public int AdminId { get; set; }

        [Column("user_id")]
        public string UserId { get; set; } = string.Empty;

        [Column("full_name")]
        public string FullName { get; set; } = "admin";

        [Column("role")]
        public string Role { get; set; } = "admin";

        [Column("active_admin")]
        public bool ActiveAdmin { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("status")]
        public string Status { get; set; } = "Active"; // Active, Suspended, Banned
    }
}
