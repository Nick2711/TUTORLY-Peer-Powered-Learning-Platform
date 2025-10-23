using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System.Text.Json.Serialization;

namespace Tutorly.Server.DatabaseModels
{
    [Table("materials")]
    public class MaterialsEntity : BaseModel
    {
        [PrimaryKey("materials_id", true)]
        public int materialsId { get; set; }

        [Column("uploaded_by_tutorid")]
        public int? uploadedByTutorid { get; set; }

        [Column("module_id")]
        public int moduleId { get; set; }

        [Column("topic_id")]
        public int? topicId { get; set; }

        [Column("type")]
        public string? type { get; set; } = null;

        [Column("url")]
        public string? url { get; set; } = null;

        [Column("description")]
        public string? description { get; set; } = null;

        [Column("created_at")]
        public DateTime createdAt { get; set; } = DateTime.Now;

        [Column("storage_provider")]
        public string storageProvider { get; set; } = "azure";

        [Column("blob_file_id")]
        public Guid Guid { get; set; } = new Guid();

        [Column("original_name")]
        public string originalName { get; set; }

        [Column("content_type")]
        public string? contentType { get; set; } = null;

        [Column("size_bytes")]
        public int? sizeBytes { get; set; } = null;

        [Column("checksum_sha256")]
        public string? checkJouMa { get; set; } = null; //wat is die

        [Column("metadata")]
        public JsonContent metaData { get; set; } = null;

        [Column("uploaded_by_studentid")]
        public string? uploadedByStudentid { get; set;} = null;

        [Column("deleted_at")]
        public DateTime? deletedAt {  get; set; } = null;
    }
}
