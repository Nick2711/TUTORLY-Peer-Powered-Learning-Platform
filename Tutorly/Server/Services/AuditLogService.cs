using Supabase;
using Tutorly.Server.DatabaseModels;
using System.Text.Json;

namespace Tutorly.Server.Services
{
    public interface IAuditLogService
    {
        Task LogEventAsync(string eventType, Guid? userId, string? entityType, string? entityId, object? details);
        Task LogBookingAttemptAsync(Guid? userId, int tutorId, int moduleId, DateTime slotStart, bool success, string? reason = null);
        Task LogBookingFailureAsync(Guid? userId, string errorCode, string errorMessage, object? details = null);
    }

    public class AuditLogService : IAuditLogService
    {
        private readonly Supabase.Client _supabase;

        public AuditLogService(Supabase.Client supabase)
        {
            _supabase = supabase;
        }

        public async Task LogEventAsync(string eventType, Guid? userId, string? entityType, string? entityId, object? details)
        {
            try
            {
                var log = new AuditLogEntity
                {
                    LogId = Guid.NewGuid(),
                    EventType = eventType,
                    UserId = userId,
                    EntityType = entityType,
                    EntityId = entityId,
                    Details = details != null ? JsonSerializer.Serialize(details) : null,
                    CreatedAt = DateTime.UtcNow
                };

                await _supabase.From<AuditLogEntity>().Insert(log);
                Console.WriteLine($"AUDIT: {eventType} - User: {userId}, Entity: {entityType}/{entityId}");
            }
            catch (Exception ex)
            {
                // Don't throw - logging failures shouldn't break the application
                Console.WriteLine($"ERROR: Failed to log audit event: {ex.Message}");
            }
        }

        public async Task LogBookingAttemptAsync(Guid? userId, int tutorId, int moduleId, DateTime slotStart, bool success, string? reason = null)
        {
            await LogEventAsync(
                success ? "BookingSuccess" : "BookingFailure",
                userId,
                "BookingRequest",
                $"{tutorId}-{moduleId}-{slotStart:O}",
                new
                {
                    TutorId = tutorId,
                    ModuleId = moduleId,
                    SlotStart = slotStart,
                    Success = success,
                    Reason = reason
                }
            );
        }

        public async Task LogBookingFailureAsync(Guid? userId, string errorCode, string errorMessage, object? details = null)
        {
            await LogEventAsync(
                "BookingFailure",
                userId,
                "BookingRequest",
                null,
                new
                {
                    ErrorCode = errorCode,
                    ErrorMessage = errorMessage,
                    Details = details
                }
            );
        }
    }
}

