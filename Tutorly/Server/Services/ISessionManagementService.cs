using Tutorly.Shared;

namespace Tutorly.Server.Services;

public interface ISessionManagementService
{
    Task<ServiceResult> CheckAndActivateScheduledSessionsAsync();
    Task<List<SessionDto>> GetUpcomingSessionsAsync(int userId, int days = 7);
    Task<ServiceResult<SessionDto>> GetSessionDetailsAsync(Guid sessionId);
    Task<ServiceResult> StartSessionAsync(Guid sessionId);
    Task<ServiceResult> EndSessionAsync(Guid sessionId);
}
