using Tutorly.Shared;

namespace Tutorly.Server.Services;

public interface IAvailabilityService
{
    Task<List<AvailabilityBlockDto>> GetTutorAvailabilityAsync(int tutorId, int? moduleId = null);
    Task<ServiceResult> SetTutorAvailabilityAsync(int tutorId, List<AvailabilityBlockDto> availabilityBlocks);
    Task<ServiceResult> AddAvailabilityExceptionAsync(int tutorId, AvailabilityExceptionDto exception);
    Task<ServiceResult> DeleteAvailabilityAsync(Guid availabilityId);
    Task<ServiceResult> SaveStudentAvailabilityAsync(StudentAvailabilityDto studentAvailability, int studentId, Guid? bookingRequestId = null);
    Task<List<AvailabilityExceptionDto>> GetTutorExceptionsAsync(int tutorId, DateTime? startDate = null, DateTime? endDate = null);
    Task<ServiceResult> DeleteAvailabilityExceptionAsync(Guid exceptionId);
}
