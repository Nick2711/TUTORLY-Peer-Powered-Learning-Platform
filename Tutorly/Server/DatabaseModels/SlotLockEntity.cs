using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Tutorly.Server.DatabaseModels
{
    [Table("slot_locks")]
    public class SlotLockEntity : BaseModel
    {
        [PrimaryKey("lock_id", false)]
        public Guid LockId { get; set; }

        [Column("tutor_id")]
        public int TutorId { get; set; }

        [Column("slot_start")]
        public DateTime SlotStart { get; set; }

        [Column("slot_end")]
        public DateTime SlotEnd { get; set; }

        [Column("locked_by_student_id")]
        public int LockedByStudentId { get; set; }

        [Column("locked_at")]
        public DateTime LockedAt { get; set; }

        [Column("expires_at")]
        public DateTime ExpiresAt { get; set; }
    }
}

