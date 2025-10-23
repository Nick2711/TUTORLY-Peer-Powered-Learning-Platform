using Tutorly.Server.DatabaseModels;
using Tutorly.Shared;

namespace Tutorly.Server.Services
{
    public class ResourceMappingService
    {
        public static Resource FromEntity(ResourceEntity entity)
        {
            // Extract filename from path if OriginalName is not available
            var resourceName = entity.ResourceName;
            if (string.IsNullOrEmpty(resourceName) && !string.IsNullOrEmpty(entity.FilePath))
            {
                resourceName = Path.GetFileName(entity.FilePath);
            }

            // Set ContentType from the entity's ContentType property
            var contentType = entity.ContentType ?? string.Empty;
            
            // If ContentType is empty, try to determine it from the file extension
            if (string.IsNullOrEmpty(contentType) && !string.IsNullOrEmpty(resourceName))
            {
                var extension = Path.GetExtension(resourceName).ToLowerInvariant();
                contentType = extension switch
                {
                    ".pdf" => "application/pdf",
                    ".doc" => "application/msword",
                    ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    ".xls" => "application/vnd.ms-excel",
                    ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    ".ppt" => "application/vnd.ms-powerpoint",
                    ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                    ".txt" => "text/plain",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".mp4" => "video/mp4",
                    ".mp3" => "audio/mpeg",
                    _ => "application/octet-stream"
                };
            }

            // Provide a reasonable default file size if not available
            var fileSize = entity.FileSize ?? GetDefaultFileSizeForExtension(resourceName);

            // Ensure CreatedAt has a valid value
            var createdAt = entity.CreatedAt;
            Console.WriteLine($"DEBUG: Raw CreatedAt from database: {entity.CreatedAt}");
            Console.WriteLine($"DEBUG: CreatedAt Kind: {entity.CreatedAt.Kind}");
            Console.WriteLine($"DEBUG: CreatedAt Ticks: {entity.CreatedAt.Ticks}");
            
            if (createdAt == default(DateTime) || createdAt.Year < 2000)
            {
                Console.WriteLine("DEBUG: Using fallback date because CreatedAt is invalid");
                createdAt = DateTime.UtcNow; // Use current time as fallback
            }
            else
            {
                // Ensure the date is treated as UTC if it's not already
                if (createdAt.Kind == DateTimeKind.Unspecified)
                {
                    createdAt = DateTime.SpecifyKind(createdAt, DateTimeKind.Utc);
                }
            }
            
            Console.WriteLine($"DEBUG: Final CreatedAt: {createdAt}");
            Console.WriteLine($"DEBUG: Final CreatedAt Kind: {createdAt.Kind}");

            return new Resource
            {
                ResourceId = entity.ResourceId,
                ResourceName = resourceName,
                ResourceType = Enum.TryParse<ResourceType>(entity.ResourceType, out var resourceType) ? resourceType : ResourceType.General,
                FilePath = entity.FilePath,
                FileSize = fileSize,
                ContentType = contentType,
                ModuleId = entity.ModuleId,
                ModuleCode = entity.ModuleCode ?? string.Empty,
                TopicId = entity.TopicId,
                UploadedBy = entity.UploadedBy,
                Description = entity.Description ?? string.Empty,
                BlobUri = entity.BlobUri,
                CreatedAt = createdAt,
                UpdatedAt = entity.DeletedAt, // Using deleted_at as updated_at for now
                IsActive = entity.IsActive
            };
        }

        private static long GetDefaultFileSizeForExtension(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return 1024; // 1KB default

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".pdf" => 2 * 1024 * 1024, // 2MB
                ".doc" or ".docx" => 500 * 1024, // 500KB
                ".xls" or ".xlsx" => 300 * 1024, // 300KB
                ".ppt" or ".pptx" => 5 * 1024 * 1024, // 5MB
                ".txt" => 10 * 1024, // 10KB
                ".jpg" or ".jpeg" or ".png" or ".gif" => 500 * 1024, // 500KB
                ".mp4" => 50 * 1024 * 1024, // 50MB
                ".mp3" => 5 * 1024 * 1024, // 5MB
                _ => 100 * 1024 // 100KB default
            };
        }

        public static ResourceEntity ToEntity(Resource resource)
        {
            var entity = new ResourceEntity
            {
                ResourceId = resource.ResourceId,
                OriginalName = resource.ResourceName,
                ResourceType = resource.ResourceType.ToString(),
                BlobFileId = Guid.TryParse(resource.FilePath, out var blobId) ? blobId : null,
                FileSize = resource.FileSize,
                ModuleId = resource.ModuleId,
                ModuleCode = resource.ModuleCode,
                TopicId = resource.TopicId,
                UploadedByTutorId = int.TryParse(resource.UploadedBy, out var tutorId) ? tutorId : null,
                Description = resource.Description,
                BlobUri = resource.BlobUri,
                CreatedAt = resource.CreatedAt,
                DeletedAt = resource.UpdatedAt,
                StorageProvider = "azure" // Default to azure
            };

            // Set ContentType as a computed property
            entity.ContentType = resource.ContentType;

            return entity;
        }

        public static List<Resource> FromEntities(List<ResourceEntity> entities)
        {
            return entities.Select(FromEntity).ToList();
        }

        public static List<ResourceEntity> ToEntities(List<Resource> resources)
        {
            return resources.Select(ToEntity).ToList();
        }
    }
}
