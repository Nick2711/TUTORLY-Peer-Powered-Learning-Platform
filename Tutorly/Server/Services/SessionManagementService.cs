using Supabase;
using Supabase.Postgrest.Models;
using Tutorly.Server.DatabaseModels;
using Tutorly.Shared;

namespace Tutorly.Server.Services;

public class SessionManagementService : ISessionManagementService
{
    private readonly Supabase.Client _supabase;
    private readonly IBookingService _bookingService;

    public SessionManagementService(Supabase.Client supabase, IBookingService bookingService)
    {
        _supabase = supabase;
        _bookingService = bookingService;
    }

    public async Task<ServiceResult> CheckAndActivateScheduledSessionsAsync()
    {
        try
        {
            var now = DateTime.UtcNow;
            var activationWindow = now.AddMinutes(5); // Activate sessions starting within next 5 minutes

            // Find sessions that should be activated
            var response = await _supabase
                .From<SessionEntity>()
                .Where(x => x.Status == "Confirmed")
                .Where(x => x.ScheduledStart <= activationWindow)
                .Where(x => x.ScheduledStart >= now)
                .Get();

            var activatedCount = 0;

            foreach (var session in response.Models)
            {
                // Update session status
                session.Status = "InProgress";
                session.UpdatedAt = DateTime.UtcNow;

                await _supabase.From<SessionEntity>().Where(x => x.SessionId == session.SessionId).Update(session);

                // Activate study room if exists
                if (session.StudyRoomId.HasValue)
                {
                    await _supabase
                        .From<StudyRoomEntity>()
                        .Where(x => x.RoomId == session.StudyRoomId.Value)
                        .Set(x => x.Status, "Active")
                        .Update();
                }

                activatedCount++;

                // TODO: Send notification to both parties via SignalR
                // await NotifySessionStarted(session);
            }

            return ServiceResult.SuccessResult($"Activated {activatedCount} sessions");
        }
        catch (Exception ex)
        {
            return ServiceResult.FailureResult($"Failed to activate sessions: {ex.Message}");
        }
    }

    public async Task<List<SessionDto>> GetUpcomingSessionsAsync(int userId, int days = 7)
    {
        try
        {
            var startDate = DateTime.UtcNow;
            var endDate = startDate.AddDays(days);

            return await _bookingService.GetUserSessionsAsync(userId, startDate, endDate);
        }
        catch (Exception ex)
        {
            return new List<SessionDto>();
        }
    }

    public async Task<ServiceResult<SessionDto>> GetSessionDetailsAsync(Guid sessionId)
    {
        try
        {
            var session = await _supabase
                .From<SessionEntity>()
                .Where(x => x.SessionId == sessionId)
                .Single();

            if (session == null)
            {
                return ServiceResult<SessionDto>.FailureResult("Session not found");
            }

            var dto = new SessionDto
            {
                SessionId = session.SessionId,
                StudentId = session.StudentId,
                TutorId = session.TutorId,
                ModuleId = session.ModuleId,
                ScheduledStart = session.ScheduledStart,
                ScheduledEnd = session.ScheduledEnd,
                StudyRoomId = session.StudyRoomId,
                Status = session.Status,
                CancellationReason = session.CancellationReason,
                CancelledAt = session.CancelledAt
            };

            // TODO: Load additional details like student name, tutor name, module info
            // This would require joins with user and module tables

            return ServiceResult<SessionDto>.SuccessResult(dto);
        }
        catch (Exception ex)
        {
            return ServiceResult<SessionDto>.FailureResult($"Failed to get session details: {ex.Message}");
        }
    }

    public async Task<ServiceResult> StartSessionAsync(Guid sessionId)
    {
        try
        {
            var session = await _supabase
                .From<SessionEntity>()
                .Where(x => x.SessionId == sessionId)
                .Single();

            if (session == null)
            {
                return ServiceResult.FailureResult("Session not found");
            }

            if (session.Status != "Confirmed")
            {
                return ServiceResult.FailureResult("Session is not in a state that can be started");
            }

            // Update session status
            session.Status = "InProgress";
            session.UpdatedAt = DateTime.UtcNow;

            await _supabase.From<SessionEntity>().Where(x => x.SessionId == sessionId).Update(session);

            // Activate study room if exists
            if (session.StudyRoomId.HasValue)
            {
                await _supabase
                    .From<StudyRoomEntity>()
                    .Where(x => x.RoomId == session.StudyRoomId.Value)
                    .Set(x => x.Status, "Active")
                    .Update();
            }

            return ServiceResult.SuccessResult();
        }
        catch (Exception ex)
        {
            return ServiceResult.FailureResult($"Failed to start session: {ex.Message}");
        }
    }

    public async Task<ServiceResult> EndSessionAsync(Guid sessionId)
    {
        try
        {
            var session = await _supabase
                .From<SessionEntity>()
                .Where(x => x.SessionId == sessionId)
                .Single();

            if (session == null)
            {
                return ServiceResult.FailureResult("Session not found");
            }

            if (session.Status != "InProgress")
            {
                return ServiceResult.FailureResult("Session is not currently in progress");
            }

            // Update session status
            session.Status = "Completed";
            session.UpdatedAt = DateTime.UtcNow;

            await _supabase.From<SessionEntity>().Where(x => x.SessionId == sessionId).Update(session);

            // End study room if exists
            if (session.StudyRoomId.HasValue)
            {
                await _supabase
                    .From<StudyRoomEntity>()
                    .Where(x => x.RoomId == session.StudyRoomId.Value)
                    .Set(x => x.Status, "Ended")
                    .Update();
            }

            return ServiceResult.SuccessResult();
        }
        catch (Exception ex)
        {
            return ServiceResult.FailureResult($"Failed to end session: {ex.Message}");
        }
    }
}
