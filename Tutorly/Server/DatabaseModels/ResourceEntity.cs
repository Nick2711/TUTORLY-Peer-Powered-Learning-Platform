using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Tutorly.Server.DatabaseModels
{
    [Table("materials")]
    public class ResourceEntity : BaseModel
    {
        [PrimaryKey("materials_id", false)]
        public string ResourceId { get; set; } = string.Empty;

        [Column("uploaded_by_tutorid")]
        public int? UploadedByTutorId { get; set; }

        [Column("module_id")]
        public int? ModuleId { get; set; }

        [Column("module_code")]
        public string? ModuleCode { get; set; }

        [Column("topic_id")]
        public int? TopicId { get; set; }

        [Column("type")]
        public string? Type { get; set; }

        [Column("resource_type")]
        public string ResourceType { get; set; } = string.Empty;

        [Column("url")]
        public string BlobUri { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("storage_provider")]
        public string? StorageProvider { get; set; }

        [Column("blob_file_id")]
        public Guid? BlobFileId { get; set; }

        [Column("original_name")]
        public string? OriginalName { get; set; }

        [Column("size_bytes")]
        public long? FileSize { get; set; }

        [Column("Etag")]
        public string? Etag { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        [Newtonsoft.Json.JsonIgnore]
        public string? ChecksumSha256 { get; set; }

        [Column("metadata")]
        [Newtonsoft.Json.JsonIgnore]
        public string? Metadata { get; set; }

        [Column("uploaded_by_studentid")]
        public int? UploadedByStudentId { get; set; }

        [Column("deleted_at")]
        public DateTime? DeletedAt { get; set; }

        //NEEEEEED to sort this (will work for now but wont store these values in the db )
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        [Newtonsoft.Json.JsonIgnore]
        public string ResourceName => OriginalName ?? string.Empty;

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        [Newtonsoft.Json.JsonIgnore]
        public string UploadedBy => UploadedByTutorId?.ToString() ?? UploadedByStudentId?.ToString() ?? string.Empty;

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        [Newtonsoft.Json.JsonIgnore]
        public string FilePath => BlobFileId?.ToString() ?? string.Empty;

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        [Newtonsoft.Json.JsonIgnore]
        public bool IsActive => DeletedAt == null;

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        [Newtonsoft.Json.JsonIgnore]
        public string? ContentType { get; set; }
    }
}


