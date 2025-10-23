using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Tutorly.Server.DatabaseModels
{
    [Table("module_tutors")]
    public class ModuleTutorEntity : BaseModel
    {
        [PrimaryKey("module_tutors_id", false)]
        public int Id { get; set; }

        [Column("tutor_id")]
        public int TutorId { get; set; }

        [Column("module_id")]
        public int ModuleId { get; set; }
    }
}


