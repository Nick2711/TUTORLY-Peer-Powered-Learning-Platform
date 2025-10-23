using Tutorly.Shared;
using System.Net.Http.Json;
using Tutorly.Client.Services;

namespace Tutorly.Client.Services
{
    public class TutorApplicationService
    {
        private readonly JwtHttpClient _httpClient;

        public TutorApplicationService(JwtHttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<ApiResponse<List<TutorApplicationDto>>> GetApplicationsAsync(TutorApplicationFilterDto? filter = null)
        {
            try
            {
                var queryParams = new List<string>();
                
                if (filter != null)
                {
                    if (!string.IsNullOrEmpty(filter.Status))
                        queryParams.Add($"status={Uri.EscapeDataString(filter.Status)}");
                    
                    if (!string.IsNullOrEmpty(filter.Programme))
                        queryParams.Add($"programme={Uri.EscapeDataString(filter.Programme)}");
                    
                    if (filter.YearOfStudy.HasValue)
                        queryParams.Add($"yearOfStudy={filter.YearOfStudy.Value}");
                    
                    if (!string.IsNullOrEmpty(filter.SearchQuery))
                        queryParams.Add($"searchQuery={Uri.EscapeDataString(filter.SearchQuery)}");
                    
                    if (!string.IsNullOrEmpty(filter.SortBy))
                        queryParams.Add($"sortBy={Uri.EscapeDataString(filter.SortBy)}");
                    
                    if (!string.IsNullOrEmpty(filter.SortOrder))
                        queryParams.Add($"sortOrder={Uri.EscapeDataString(filter.SortOrder)}");
                }

                var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
                var response = await _httpClient.GetFromJsonAsync<ApiResponse<List<TutorApplicationDto>>>($"api/tutorapplication/admin/applications{queryString}");
                
                return response ?? new ApiResponse<List<TutorApplicationDto>> { Success = false, Message = "Failed to load applications" };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting tutor applications: {ex.Message}");
                return new ApiResponse<List<TutorApplicationDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<TutorApplicationDto>> GetApplicationAsync(int applicationId)
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<ApiResponse<TutorApplicationDto>>($"api/tutorapplication/{applicationId}");
                return response ?? new ApiResponse<TutorApplicationDto> { Success = false, Message = "Failed to load application" };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting tutor application: {ex.Message}");
                return new ApiResponse<TutorApplicationDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<bool>> ReviewApplicationAsync(int applicationId, TutorApplicationReviewDto reviewDto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"api/tutorapplication/admin/{applicationId}/review", reviewDto);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
                    return result ?? new ApiResponse<bool> { Success = false, Message = "Failed to review application" };
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return new ApiResponse<bool> { Success = false, Message = $"Error: {response.StatusCode} - {errorContent}" };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reviewing tutor application: {ex.Message}");
                return new ApiResponse<bool> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<bool>> ApproveApplicationAsync(int applicationId, string? adminNotes = null)
        {
            var reviewDto = new TutorApplicationReviewDto
            {
                Status = "Approved",
                AdminNotes = adminNotes
            };
            return await ReviewApplicationAsync(applicationId, reviewDto);
        }

        public async Task<ApiResponse<bool>> RejectApplicationAsync(int applicationId, string? adminNotes = null)
        {
            var reviewDto = new TutorApplicationReviewDto
            {
                Status = "Rejected",
                AdminNotes = adminNotes
            };
            return await ReviewApplicationAsync(applicationId, reviewDto);
        }
    }
}
