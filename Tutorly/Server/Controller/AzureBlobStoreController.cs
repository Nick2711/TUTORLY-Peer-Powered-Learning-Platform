using Microsoft.AspNetCore.Mvc;
using Tutorly.Server.Handler;
using Tutorly.Server.Helpers;
using Tutorly.Server.DatabaseModels;
using Tutorly.Server.Services;
using Tutorly.Shared;
using Azure.Core;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Tutorly.Server.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class AzureBlobStoreController : ControllerBase
    {
        private readonly AzureBlobStoreHandler _blobHandler;
        private readonly SupabaseClientFactory _clientFactory;
        private readonly AzureBlobStoreOptions _blobOptions;

        public AzureBlobStoreController(AzureBlobStoreHandler blobHandler, SupabaseClientFactory clientFactory, IOptions<AzureBlobStoreOptions> blobOptions)
        {
            _blobHandler = blobHandler;
            _clientFactory = clientFactory;
            _blobOptions = blobOptions.Value;
        }

        // POST: api/azureblobstore/upload
        [HttpPost("upload")]
        public async Task<IActionResult> UploadResource([FromForm] IFormFile file, [FromForm] ResourceUploadRequest request)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest("No file provided");

                // Validate file size (max 50MB)
                if (file.Length > 50 * 1024 * 1024)
                    return BadRequest("File size exceeds 50MB limit");

                // Generate unique GUID for ResourceId
                var resourceGuid = Guid.NewGuid().ToString();
                var fileExtension = Path.GetExtension(file.FileName);
                var uniqueFileName = $"{resourceGuid}{fileExtension}";

                // Create folder structure based on resource type and module code
                var folderPath = GetFolderPath(request.ResourceType, request.ModuleCode, request.TopicId);
                var fullPath = $"{folderPath}/{uniqueFileName}";

                // Upload to Azure Blob Storage
                using var stream = file.OpenReadStream();

                Uri blobUri;
                try
                {
                    blobUri = await _blobHandler.PutAsync(stream, fullPath, true);
                }
                catch (Exception blobEx)
                {
                    // Fallback: Save to local temp directory for development
                    var tempDir = Path.Combine(Path.GetTempPath(), "tutorly-resources");
                    System.IO.Directory.CreateDirectory(tempDir);
                    var localPath = Path.Combine(tempDir, uniqueFileName);

                    using var fileStream = System.IO.File.Create(localPath);
                    stream.Position = 0;
                    await stream.CopyToAsync(fileStream);

                    blobUri = new Uri($"file://{localPath}");
                }

                // Save resource metadata to database
                var resourceEntity = new ResourceEntity
                {
                    ResourceId = resourceGuid,
                    OriginalName = file.FileName,
                    ResourceType = request.ResourceType.ToString(),
                    BlobFileId = Guid.NewGuid(), // Generate a new GUID for blob_file_id
                    FileSize = file.Length,
                    ModuleId = request.ModuleId,
                    ModuleCode = request.ModuleCode,
                    TopicId = request.TopicId,
                    UploadedByTutorId = int.TryParse(request.UploadedBy, out var tutorId) ? tutorId : null,
                    Description = request.Description,
                    BlobUri = blobUri.ToString(),
                    CreatedAt = DateTime.UtcNow,
                    StorageProvider = "azure"
                };

                // Set ContentType as a computed property (not stored in database)
                resourceEntity.ContentType = file.ContentType;

                await _clientFactory.AddEntity(resourceEntity);

                var response = new ResourceUploadResponse
                {
                    ResourceId = resourceEntity.ResourceId,
                    ResourceName = resourceEntity.ResourceName,
                    FileSize = file.Length,
                    ContentType = file.ContentType,
                    BlobUri = blobUri.ToString(),
                    FilePath = resourceEntity.FilePath,
                    UploadedAt = resourceEntity.CreatedAt
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error uploading resource", error = ex.Message });
            }
        }

        // PUT: api/azureblobstore/{resourceId}/metadata
        [HttpPut("{resourceId}/metadata")]
        public async Task<IActionResult> UpdateResourceMetadata(string resourceId, [FromBody] ResourceMetadataUpdateRequest request)
        {
            try
            {
                var resource = await GetResourceById(resourceId);
                if (resource == null)
                    return NotFound("Resource not found");

                // Update resource metadata using Supabase client
                var client = _clientFactory.CreateService();
                
                await client
                    .From<ResourceEntity>()
                    .Where(x => x.ResourceId == resourceId)
                    .Set(x => x.Description, request.Description)
                    .Set(x => x.CreatedAt, request.UpdatedAt ?? DateTime.UtcNow) // Using CreatedAt as UpdatedAt for now
                    .Update();

                return Ok(new { message = "Resource metadata updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating resource metadata", error = ex.Message });
            }
        }

        // GET: api/azureblobstore/{resourceId}/download
        [HttpGet("{resourceId}/download")]
        public async Task<IActionResult> DownloadResource(string resourceId)
        {
            try
            {
                var resource = await GetResourceById(resourceId);
                if (resource == null)
                    return NotFound("Resource not found");

                // Determine content type from file extension if not available
                var contentType = resource.ContentType;
                if (string.IsNullOrEmpty(contentType) && !string.IsNullOrEmpty(resource.ResourceName))
                {
                    var extension = Path.GetExtension(resource.ResourceName).ToLowerInvariant();
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

                // Use default content type if still empty
                if (string.IsNullOrEmpty(contentType))
                    contentType = "application/octet-stream";

                // Handle case where FilePath (BlobFileId) is empty but BlobUri exists
                string blobPath = resource.FilePath;
                if (string.IsNullOrEmpty(blobPath) && !string.IsNullOrEmpty(resource.BlobUri))
                {
                    // Extract blob path from the BlobUri
                    try
                    {
                        var uri = new Uri(resource.BlobUri);
                        blobPath = uri.AbsolutePath.TrimStart('/');
                        
                        // Remove container name from the path if it's included
                        var containerName = _blobOptions.BlobContainerName;
                        if (blobPath.StartsWith($"{containerName}/"))
                        {
                            blobPath = blobPath.Substring(containerName.Length + 1);
                        }
                        
                        // URL decode the blob path to handle spaces and special characters
                        blobPath = Uri.UnescapeDataString(blobPath);
                    }
                    catch (Exception uriEx)
                    {
                        return StatusCode(500, new { message = "Error parsing resource URI", error = uriEx.Message });
                    }
                }

                if (string.IsNullOrEmpty(blobPath))
                    return NotFound("File path not found for resource");

                var stream = await _blobHandler.GetAsStreamAsync(blobPath);

                return new FileStreamResult(stream, contentType)
                {
                    FileDownloadName = resource.ResourceName
                };
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error downloading resource", error = ex.Message });
            }
        }

        // GET: api/azureblobstore/{resourceId}/url
        [HttpGet("{resourceId}/url")]
        public async Task<IActionResult> GetResourceUrl(string resourceId)
        {
            try
            {
                var resource = await GetResourceById(resourceId);
                if (resource == null)
                    return NotFound("Resource not found");

                var url = await _blobHandler.GetUriAsync(resource.FilePath);

                return Ok(new { url = url.ToString() });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error getting resource URL", error = ex.Message });
            }
        }

        // GET: api/azureblobstore/module/{moduleId}
        [HttpGet("module/{moduleId}")]
        public async Task<IActionResult> GetModuleResources(int moduleId)
        {
            try
            {
                Console.WriteLine($"DEBUG: GetModuleResources - START - ModuleId={moduleId}");
                Console.WriteLine($"DEBUG: GetModuleResources - BlobHandler: {_blobHandler != null}");
                Console.WriteLine($"DEBUG: GetModuleResources - ClientFactory: {_clientFactory != null}");

                var resourceEntities = await GetResourcesByModuleId(moduleId);
                Console.WriteLine($"DEBUG: GetModuleResources - Found {resourceEntities.Count} entities");

                var resources = ResourceMappingService.FromEntities(resourceEntities);
                Console.WriteLine($"DEBUG: GetModuleResources - Mapped to {resources.Count} resources");

                Console.WriteLine($"DEBUG: GetModuleResources - SUCCESS - Returning {resources.Count} resources");
                return Ok(resources);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: GetModuleResources - EXCEPTION: {ex.Message}");
                Console.WriteLine($"DEBUG: GetModuleResources - StackTrace: {ex.StackTrace}");
                Console.WriteLine($"DEBUG: GetModuleResources - InnerException: {ex.InnerException?.Message}");
                return StatusCode(500, new { message = "Error retrieving module resources", error = ex.Message });
            }
        }

        // GET: api/azureblobstore/module-code/{moduleCode}
        [HttpGet("module-code/{moduleCode}")]
        public async Task<IActionResult> GetModuleResourcesByCode(string moduleCode)
        {
            try
            {
                var resourceEntities = await GetResourcesByModuleCode(moduleCode);
                var resources = ResourceMappingService.FromEntities(resourceEntities);
                return Ok(resources);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving module resources", error = ex.Message });
            }
        }

        // GET: api/azureblobstore/topic/{topicId}
        [HttpGet("topic/{topicId}")]
        public async Task<IActionResult> GetTopicResources(int topicId)
        {
            try
            {
                var resourceEntities = await GetResourcesByTopicId(topicId);
                var resources = ResourceMappingService.FromEntities(resourceEntities);
                return Ok(resources);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving topic resources", error = ex.Message });
            }
        }

        // DELETE: api/azureblobstore/{resourceId}
        [HttpDelete("{resourceId}")]
        public async Task<IActionResult> DeleteResource(string resourceId)
        {
            try
            {
                var resource = await GetResourceById(resourceId);
                if (resource == null)
                    return NotFound("Resource not found");

                // Delete from Azure Blob Storage
                await _blobHandler.DeleteAsync(resource.FilePath);

                // Delete from database
                //await _clientFactory.DeleteEntity<ResourceEntity>(resourceId);

                return Ok(new { message = "Resource deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error deleting resource", error = ex.Message });
            }
        }

        // GET: api/azureblobstore/files
        [HttpGet("files")]
        public async Task<IActionResult> GetAllFiles()
        {
            try
            {
                var files = await _blobHandler.GetAllAsync();
                return Ok(files);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving files", error = ex.Message });
            }
        }

        // GET: api/azureblobstore/exists/{filename}
        [HttpGet("exists/{filename}")]
        public async Task<IActionResult> FileExists(string filename)
        {
            try
            {
                var exists = await _blobHandler.ContainsAsync(filename);
                return Ok(new { exists = exists });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error checking file existence", error = ex.Message });
            }
        }

        private string GetFolderPath(Tutorly.Shared.ResourceType resourceType, string moduleCode, int? topicId)
        {
            var basePath = resourceType switch
            {
                Tutorly.Shared.ResourceType.ModuleResource => $"modules/{moduleCode}",
                Tutorly.Shared.ResourceType.ForumAttachment => $"forum/{topicId}",
                Tutorly.Shared.ResourceType.MessageAttachment => "messages",
                Tutorly.Shared.ResourceType.ProfilePicture => "profiles",
                _ => "general"
            };

            return basePath;
        }

        private async Task<ResourceEntity?> GetResourceById(string resourceId)
        {
            var entities = await _clientFactory.GetEntities<ResourceEntity>();
            return entities.FirstOrDefault(r => r.ResourceId == resourceId);
        }

        private async Task<List<ResourceEntity>> GetResourcesByModuleId(int moduleId)
        {
            try
            {
                Console.WriteLine($"DEBUG: GetResourcesByModuleId - ModuleId={moduleId}");

                var entities = await _clientFactory.GetEntities<ResourceEntity>();
                Console.WriteLine($"DEBUG: GetResourcesByModuleId - Total entities: {entities.Count}");

                var filtered = entities.Where(r => r.ModuleId == moduleId && r.IsActive).ToList();
                Console.WriteLine($"DEBUG: GetResourcesByModuleId - Filtered entities: {filtered.Count}");

                return filtered;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: GetResourcesByModuleId - Exception: {ex.Message}");
                Console.WriteLine($"DEBUG: GetResourcesByModuleId - StackTrace: {ex.StackTrace}");
                Console.WriteLine($"DEBUG: GetResourcesByModuleId - InnerException: {ex.InnerException?.Message}");

                // Return empty list on error to prevent 500 errors
                return new List<ResourceEntity>();
            }
        }

        private async Task<List<ResourceEntity>> GetResourcesByTopicId(int topicId)
        {
            var entities = await _clientFactory.GetEntities<ResourceEntity>();
            return entities.Where(r => r.TopicId == topicId).ToList();
        }

        private async Task<List<ResourceEntity>> GetResourcesByModuleCode(string moduleCode)
        {
            var entities = await _clientFactory.GetEntities<ResourceEntity>();
            return entities.Where(r => r.ModuleCode == moduleCode).ToList();
        }

        // GET: api/azureblobstore/student/{studentId}/tutor-resources
        [HttpGet("student/{studentId}/tutor-resources")]
        public async Task<IActionResult> GetTutorResourcesForStudent(int studentId)
        {
            try
            {
                // Get student's enrolled modules
                var studentModules = await _clientFactory.GetEntities<ModuleStudentEntity>();
                var enrolledModuleIds = studentModules
                    .Where(sm => sm.StudentId == studentId)
                    .Select(sm => sm.ModuleId)
                    .ToList();

                if (!enrolledModuleIds.Any())
                {
                    return Ok(new List<Tutorly.Shared.Resource>());
                }

                // Get tutors for these modules
                var moduleTutors = await _clientFactory.GetEntities<ModuleTutorEntity>();
                var tutorIds = moduleTutors
                    .Where(mt => enrolledModuleIds.Contains(mt.ModuleId))
                    .Select(mt => mt.TutorId.ToString())
                    .ToList();

                // Get module codes for the enrolled modules
                var modules = await _clientFactory.GetEntities<ModuleEntity>();
                var moduleCodes = modules
                    .Where(m => enrolledModuleIds.Contains(m.ModuleId))
                    .Select(m => m.ModuleCode)
                    .ToList();

                // Get resources uploaded by tutors for these module codes
                var resourceEntities = await _clientFactory.GetEntities<ResourceEntity>();
                var tutorResources = resourceEntities
                    .Where(r => moduleCodes.Contains(r.ModuleCode) &&
                               tutorIds.Contains(r.UploadedBy) &&
                               r.ResourceType == Tutorly.Shared.ResourceType.ModuleResource.ToString())
                    .ToList();

                var resources = ResourceMappingService.FromEntities(tutorResources);
                return Ok(resources);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving tutor resources for student", error = ex.Message });
            }
        }

        // GET: api/azureblobstore/tutor/{tutorId}/my-resources
        [HttpGet("tutor/{tutorId}/my-resources")]
        public async Task<IActionResult> GetTutorResources(int tutorId)
        {
            try
            {
                // Get tutor's assigned modules
                var moduleTutors = await _clientFactory.GetEntities<ModuleTutorEntity>();
                var assignedModuleIds = moduleTutors
                    .Where(mt => mt.TutorId == tutorId)
                    .Select(mt => mt.ModuleId)
                    .ToList();

                if (!assignedModuleIds.Any())
                {
                    return Ok(new List<Tutorly.Shared.Resource>());
                }

                // Get module codes for the assigned modules
                var modules = await _clientFactory.GetEntities<ModuleEntity>();
                var moduleCodes = modules
                    .Where(m => assignedModuleIds.Contains(m.ModuleId))
                    .Select(m => m.ModuleCode)
                    .ToList();

                // Get resources uploaded by this tutor for these modules
                var resourceEntities = await _clientFactory.GetEntities<ResourceEntity>();
                var tutorResources = resourceEntities
                    .Where(r => moduleCodes.Contains(r.ModuleCode) &&
                               r.UploadedBy == tutorId.ToString() &&
                               r.ResourceType == Tutorly.Shared.ResourceType.ModuleResource.ToString())
                    .ToList();

                var resources = ResourceMappingService.FromEntities(tutorResources);
                return Ok(resources);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving tutor resources", error = ex.Message });
            }
        }

        // GET: api/azureblobstore/student/{studentId}/resources-by-module
        [HttpGet("student/{studentId}/resources-by-module")]
        public async Task<IActionResult> GetTutorResourcesByModuleForStudent(int studentId)
        {
            try
            {
                // Get student's enrolled modules with details
                var studentModules = await _clientFactory.GetEntities<ModuleStudentEntity>();
                var enrolledModuleIds = studentModules
                    .Where(sm => sm.StudentId == studentId)
                    .Select(sm => sm.ModuleId)
                    .ToList();

                if (!enrolledModuleIds.Any())
                {
                    return Ok(new Dictionary<string, object>());
                }

                // Get module details
                var modules = await _clientFactory.GetEntities<ModuleEntity>();
                var enrolledModules = modules
                    .Where(m => enrolledModuleIds.Contains(m.ModuleId))
                    .ToList();

                // Get tutors for these modules
                var moduleTutors = await _clientFactory.GetEntities<ModuleTutorEntity>();
                var tutorIds = moduleTutors
                    .Where(mt => enrolledModuleIds.Contains(mt.ModuleId))
                    .Select(mt => mt.TutorId.ToString())
                    .ToList();

                var moduleCodes = enrolledModules.Select(m => m.ModuleCode).ToList();

                // Get resources uploaded by tutors for these module codes
                var resourceEntities = await _clientFactory.GetEntities<ResourceEntity>();
                var tutorResources = resourceEntities
                    .Where(r => moduleCodes.Contains(r.ModuleCode) &&
                               tutorIds.Contains(r.UploadedBy) &&
                               r.ResourceType == Tutorly.Shared.ResourceType.ModuleResource.ToString())
                    .ToList();

                // Group resources by module code
                var result = new Dictionary<string, object>();

                foreach (var module in enrolledModules)
                {
                    var moduleResources = ResourceMappingService.FromEntities(
                        tutorResources.Where(r => r.ModuleCode == module.ModuleCode).ToList());

                    // Debug: Log the first resource's date
                    if (moduleResources.Any())
                    {
                        var firstResource = moduleResources.First();
                        Console.WriteLine($"DEBUG: API - First resource CreatedAt: {firstResource.CreatedAt}");
                        Console.WriteLine($"DEBUG: API - First resource CreatedAt Kind: {firstResource.CreatedAt.Kind}");
                    }

                    result[module.ModuleCode] = new
                    {
                        ModuleId = module.ModuleId,
                        ModuleCode = module.ModuleCode,
                        ModuleName = module.ModuleName,
                        ModuleDescription = module.ModuleDescription,
                        Resources = moduleResources,
                        ResourceCount = moduleResources.Count
                    };
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving tutor resources by module for student", error = ex.Message });
            }
        }
    }
}