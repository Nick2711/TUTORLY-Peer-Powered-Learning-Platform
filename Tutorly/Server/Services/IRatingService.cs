using Tutorly.Shared;

namespace Tutorly.Server.Services
{
    public interface IRatingService
    {
        Task<ServiceResult<TutorRatingDto>> CreateRatingAsync(CreateTutorRatingDto ratingDto);
        Task<ServiceResult<TutorRatingDto>> UpdateRatingAsync(Guid ratingId, CreateTutorRatingDto ratingDto);
        Task<ServiceResult> DeleteRatingAsync(Guid ratingId);
        Task<ServiceResult<List<TutorRatingDto>>> GetTutorRatingsAsync(int tutorId);
        Task<ServiceResult<List<TutorRatingDto>>> GetStudentRatingsAsync(int studentId);
        Task<ServiceResult<TutorRatingSummaryDto>> GetTutorRatingSummaryAsync(int tutorId);
        Task<ServiceResult<TutorRatingDto?>> GetExistingRatingAsync(int tutorId, int studentId, int moduleId);
    }
}
