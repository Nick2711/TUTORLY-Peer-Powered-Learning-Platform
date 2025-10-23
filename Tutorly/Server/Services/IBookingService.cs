using Tutorly.Shared;

namespace Tutorly.Server.Services;

public interface IBookingService
{
    Task<List<BookableSlotDto>> PreviewBookableSlotsAsync(int studentId, int tutorId, int moduleId, DateTime startDate, DateTime endDate, StudentAvailabilityDto studentAvailability);
    Task<ServiceResult<BookingRequestDto>> CreateBookingRequestAsync(BookingRequestDto request);
    Task<ServiceResult<List<SessionDto>>> ConfirmBookingAsync(Guid requestId, List<DateTime> approvedSlots, int tutorId);
    Task<ServiceResult> CancelSessionAsync(Guid sessionId, int userId, string reason);
    Task<List<BookingRequestDto>> GetPendingRequestsAsync(int tutorId);
    Task<List<BookingRequestDto>> GetPendingBookingRequestsAsync(int tutorId);
    Task<ServiceResult> ConfirmBookingRequestAsync(Guid requestId);
    Task<ServiceResult> RejectBookingRequestAsync(Guid requestId);
    Task<List<SessionDto>> GetUserSessionsAsync(int userId, DateTime? startDate = null, DateTime? endDate = null);
    Task<List<CalendarEventDto>> GetCalendarEventsAsync(int userId, DateTime startDate, DateTime endDate);
    Task<List<UpcomingSessionDto>> GetUpcomingSessionsAsync(int userId, int limit = 3);
    Task<TutorAnalyticsDto> GetTutorAnalyticsAsync(int tutorId, int days = 30);
    Task<ServiceResult<ModuleTutorPreferencesDto>> GetModuleTutorPreferencesAsync(int tutorId, int moduleId);
    Task<ServiceResult> SetModuleTutorPreferencesAsync(ModuleTutorPreferencesDto preferences);
    Task<object> GetDatabaseStatusAsync();
}
