using Supabase;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System.Text.Json.Serialization;

namespace Tutorly.Server.DatabaseModels
{
    [Table("modules")]
    public class ModuleEntity : BaseModel
    {
        [PrimaryKey("module_id", true)] //auto increments - not UUId in Supabase
        public int ModuleId { get; set; }

        [Column("module_code")]
        public string ModuleCode { get; set; } = string.Empty;

        [Column("module_name")]
        public string ModuleName { get; set; } = string.Empty;

        [Column("module_description")]
        public string ModuleDescription { get; set; } = string.Empty;
    }

    // Separate class for inserts that doesn't include ModuleId
    [Table("modules")]
    public class ModuleInsertEntity : BaseModel
    {
        // No primary key - this is for inserts only
        [Column("module_code")]
        public string ModuleCode { get; set; } = string.Empty;

        [Column("module_name")]
        public string ModuleName { get; set; } = string.Empty;

        [Column("module_description")]
        public string ModuleDescription { get; set; } = string.Empty;
    }
}

