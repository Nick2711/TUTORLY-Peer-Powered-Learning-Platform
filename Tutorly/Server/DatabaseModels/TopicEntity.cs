using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Tutorly.Server.DatabaseModels
{
    public class TopicEntity : BaseModel
    {
        public TopicEntity() { }  // parameterless constructor

        [PrimaryKey("id", false)]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Column("created_by")]
        public string CreatedBy { get; set; }

        [Column("module_id")]
        public string ModuleId { get; set; }
    }

}
