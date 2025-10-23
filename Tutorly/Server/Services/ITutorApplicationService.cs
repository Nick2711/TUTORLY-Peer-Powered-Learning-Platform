using Tutorly.Shared;

namespace Tutorly.Server.Services
{
    public interface ITutorApplicationService
    {
        Task<ApiResponse<TutorApplicationDto>> CreateApplicationAsync(CreateTutorApplicationDto dto, string userId, IFormFile? transcriptFile);
        Task<ApiResponse<TutorApplicationDto>> GetApplicationByIdAsync(int applicationId);
        Task<ApiResponse<TutorApplicationDto>> GetApplicationByUserIdAsync(string userId);
        Task<ApiResponse<List<TutorApplicationDto>>> GetApplicationsAsync(TutorApplicationFilterDto filter);
        Task<ApiResponse<TutorApplicationDto>> UpdateApplicationAsync(int applicationId, UpdateTutorApplicationDto dto, string userId);
        Task<ApiResponse<bool>> DeleteApplicationAsync(int applicationId, string userId);
        Task<ApiResponse<TutorApplicationDto>> ReviewApplicationAsync(int applicationId, TutorApplicationReviewDto dto, string adminUserId);
        Task<ApiResponse<string>> UploadTranscriptAsync(int applicationId, IFormFile transcriptFile);
        Task<ApiResponse<bool>> DeleteTranscriptAsync(int applicationId);
    }
}
