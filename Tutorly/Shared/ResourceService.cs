using System.Net.Http.Json;

namespace Tutorly.Shared
{
    public class ResourceService
    {
        private readonly HttpClient _httpClient;

        public ResourceService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<ResourceUploadResponse?> UploadResourceAsync(Stream fileStream, string fileName, string contentType, ResourceUploadRequest request)
        {
            try
            {
                using var formData = new MultipartFormDataContent();
                
                formData.Add(new StreamContent(fileStream), "file", fileName);
                formData.Add(new StringContent(request.ResourceType.ToString()), "resourceType");
                formData.Add(new StringContent(request.ModuleCode), "moduleCode");
                
                if (request.ModuleId.HasValue)
                    formData.Add(new StringContent(request.ModuleId.Value.ToString()), "moduleId");
                
                if (request.TopicId.HasValue)
                    formData.Add(new StringContent(request.TopicId.Value.ToString()), "topicId");
                
                formData.Add(new StringContent(request.UploadedBy), "uploadedBy");
                formData.Add(new StringContent(request.Description), "description");

                var response = await _httpClient.PostAsync("api/azureblobstore/upload", formData);
                
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<ResourceUploadResponse>();
                }
                
                return null;
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<Resource>> GetModuleResourcesAsync(int moduleId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/azureblobstore/module/{moduleId}");
                if (response.IsSuccessStatusCode)
                {
                    var resources = await response.Content.ReadFromJsonAsync<List<Resource>>();
                    return resources ?? new List<Resource>();
                }
                return new List<Resource>();
            }
            catch
            {
                return new List<Resource>();
            }
        }

        public async Task<List<Resource>> GetModuleResourcesByCodeAsync(string moduleCode)
        {
            try
            {
                var resources = await _httpClient.GetFromJsonAsync<List<Resource>>($"api/azureblobstore/module-code/{moduleCode}");
                return resources ?? new List<Resource>();
            }
            catch
            {
                return new List<Resource>();
            }
        }

        public async Task<List<Resource>> GetTutorResourcesForStudentAsync(int studentId)
        {
            try
            {
                var resources = await _httpClient.GetFromJsonAsync<List<Resource>>($"api/azureblobstore/student/{studentId}/tutor-resources");
                return resources ?? new List<Resource>();
            }
            catch
            {
                return new List<Resource>();
            }
        }

        public async Task<Dictionary<string, object>> GetTutorResourcesByModuleForStudentAsync(int studentId)
        {
            try
            {
                var resources = await _httpClient.GetFromJsonAsync<Dictionary<string, object>>($"api/azureblobstore/student/{studentId}/resources-by-module");
                return resources ?? new Dictionary<string, object>();
            }
            catch
            {
                return new Dictionary<string, object>();
            }
        }

        public async Task<List<Resource>> GetTopicResourcesAsync(int topicId)
        {
            try
            {
                var resources = await _httpClient.GetFromJsonAsync<List<Resource>>($"api/azureblobstore/topic/{topicId}");
                return resources ?? new List<Resource>();
            }
            catch
            {
                return new List<Resource>();
            }
        }

        public async Task<string?> GetResourceUrlAsync(string resourceId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/azureblobstore/{resourceId}/url");
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<dynamic>();
                    return result?.url;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public async Task<Stream?> DownloadResourceAsync(string resourceId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/azureblobstore/{resourceId}/download");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStreamAsync();
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> DeleteResourceAsync(string resourceId)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"api/azureblobstore/{resourceId}");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UpdateResourceMetadataAsync(string resourceId, string title, string description, string tags, string version)
        {
            try
            {
                var updateData = new
                {
                    title,
                    description,
                    tags,
                    version,
                    updatedAt = DateTime.UtcNow
                };

                var response = await _httpClient.PutAsJsonAsync($"api/azureblobstore/{resourceId}/metadata", updateData);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public string GetResourceDownloadUrl(string resourceId)
        {
            return $"api/azureblobstore/{resourceId}/download";
        }
    }
}
