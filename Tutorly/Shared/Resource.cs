
namespace Tutorly.Shared
{
    public enum ResourceType
    {
        ModuleResource,
        ForumAttachment,
        MessageAttachment,
        ProfilePicture,
        General
    }

    public class Resource
    {
        public string ResourceId { get; set; } = string.Empty;
        public string ResourceName { get; set; } = string.Empty;
        public ResourceType ResourceType { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public int? ModuleId { get; set; }
        public string ModuleCode { get; set; } = string.Empty;
        public int? TopicId { get; set; }
        public string UploadedBy { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string BlobUri { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;

        // Helper properties
        public string FileSizeFormatted => FormatFileSize(FileSize);
        public string FileIcon => GetFileIcon(ContentType);
        public bool IsImage => ContentType.StartsWith("image/");
        public bool IsDocument => IsPdf() || IsWord() || IsExcel() || IsPowerPoint();
        public bool IsVideo => ContentType.StartsWith("video/");
        public bool IsAudio => ContentType.StartsWith("audio/");

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private string GetFileIcon(string contentType)
        {
            return contentType switch
            {
                string ct when ct.StartsWith("image/") => "🖼️",
                string ct when ct.StartsWith("video/") => "🎥",
                string ct when ct.StartsWith("audio/") => "🎵",
                string ct when ct.Contains("pdf") => "📄",
                string ct when ct.Contains("word") || ct.Contains("document") => "📝",
                string ct when ct.Contains("excel") || ct.Contains("spreadsheet") => "📊",
                string ct when ct.Contains("powerpoint") || ct.Contains("presentation") => "📊",
                string ct when ct.Contains("zip") || ct.Contains("rar") || ct.Contains("archive") => "📦",
                string ct when ct.Contains("text/") => "📄",
                _ => "📎"
            };
        }

        private bool IsPdf() => ContentType.Contains("pdf");
        private bool IsWord() => ContentType.Contains("word") || ContentType.Contains("document");
        private bool IsExcel() => ContentType.Contains("excel") || ContentType.Contains("spreadsheet");
        private bool IsPowerPoint() => ContentType.Contains("powerpoint") || ContentType.Contains("presentation");

    }

    public class ResourceUploadRequest
    {
        public ResourceType ResourceType { get; set; }
        public int? ModuleId { get; set; }
        public string ModuleCode { get; set; } = string.Empty;
        public int? TopicId { get; set; }
        public string UploadedBy { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class ResourceMetadataUpdateRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Tags { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public DateTime? UpdatedAt { get; set; }
    }

    public class ResourceUploadResponse
    {
        public string ResourceId { get; set; } = string.Empty;
        public string ResourceName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public string BlobUri { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
    }
}

