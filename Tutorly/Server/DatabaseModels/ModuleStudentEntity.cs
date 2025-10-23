using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Tutorly.Server.DatabaseModels
{
    [Table("module_student")]
    public class ModuleStudentEntity : BaseModel
    {
        [PrimaryKey("module_student_id", false)]
        public int Id { get; set; }

        [Column("student_id")]
        public int StudentId { get; set; }

        [Column("module_id")]
        public int ModuleId { get; set; }
    }
}


