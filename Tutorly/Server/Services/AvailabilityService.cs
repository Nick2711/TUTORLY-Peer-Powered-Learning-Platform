using Supabase;
using Supabase.Postgrest.Models;
using Tutorly.Server.DatabaseModels;
using Tutorly.Shared;

namespace Tutorly.Server.Services;

public class AvailabilityService : IAvailabilityService
{
    private readonly Supabase.Client _supabase;

    public AvailabilityService(Supabase.Client supabase)
    {
        _supabase = supabase;
    }

    public async Task<List<AvailabilityBlockDto>> GetTutorAvailabilityAsync(int tutorId, int? moduleId = null)
    {
        try
        {
            Console.WriteLine($"DEBUG: GetTutorAvailabilityAsync - TutorId={tutorId}, ModuleId={moduleId}");

            var query = _supabase
                .From<TutorAvailabilityEntity>()
                .Where(x => x.TutorId == tutorId);

            if (moduleId.HasValue)
            {
                // Handle data inconsistency: some records may have empty strings instead of null
                // We'll get all records for the tutor and filter in memory to avoid SQL parsing errors
                Console.WriteLine($"DEBUG: Skipping module filter due to data inconsistency - will filter in memory");
            }

            var response = await query.Get();
            var entities = response.Models;

            Console.WriteLine($"DEBUG: GetTutorAvailabilityAsync - Found {entities.Count} entities");
            foreach (var entity in entities)
            {
                Console.WriteLine($"DEBUG: Entity - TutorId={entity.TutorId}, ModuleId={entity.ModuleId}, Day={entity.DayOfWeek}, Start={entity.StartTime}, End={entity.EndTime}");
            }

            // Filter in memory to handle data inconsistency (empty strings vs null)
            var filteredEntities = entities;
            if (moduleId.HasValue)
            {
                filteredEntities = entities.Where(e => e.ModuleId == moduleId || e.ModuleId == null).ToList();
                Console.WriteLine($"DEBUG: After in-memory filtering - {filteredEntities.Count} entities match module {moduleId}");
            }

            var result = filteredEntities.Select(MapToDto).ToList();
            Console.WriteLine($"DEBUG: GetTutorAvailabilityAsync - Returning {result.Count} DTOs");

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: GetTutorAvailabilityAsync - Exception: {ex.Message}");
            return new List<AvailabilityBlockDto>();
        }
    }

    public async Task<ServiceResult> SetTutorAvailabilityAsync(int tutorId, List<AvailabilityBlockDto> availabilityBlocks)
    {
        try
        {
            // Delete existing availability for this tutor
            await _supabase
                .From<TutorAvailabilityEntity>()
                .Where(x => x.TutorId == tutorId)
                .Delete();

            // Insert new availability blocks
            var entities = availabilityBlocks.Select(dto => new TutorAvailabilityEntity
            {
                AvailabilityId = dto.AvailabilityId ?? Guid.NewGuid(),
                TutorId = tutorId,
                ModuleId = dto.ModuleId,
                DayOfWeek = dto.DayOfWeek,
                StartTime = TimeSpan.Parse(dto.StartTime),
                EndTime = TimeSpan.Parse(dto.EndTime),
                IsRecurring = dto.IsRecurring,
                EffectiveFrom = dto.EffectiveFrom,
                EffectiveUntil = dto.EffectiveUntil,
                Timezone = dto.Timezone,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ToList();

            await _supabase.From<TutorAvailabilityEntity>().Insert(entities);

            return ServiceResult.SuccessResult();
        }
        catch (Exception ex)
        {
            return ServiceResult.FailureResult($"Failed to set tutor availability: {ex.Message}");
        }
    }

    public async Task<ServiceResult> AddAvailabilityExceptionAsync(int tutorId, AvailabilityExceptionDto exception)
    {
        try
        {
            var entity = new TutorAvailabilityExceptionEntity
            {
                ExceptionId = exception.ExceptionId ?? Guid.NewGuid(),
                TutorId = tutorId,
                AvailabilityId = exception.AvailabilityId,
                ExceptionDate = exception.ExceptionDate,
                IsAvailable = exception.IsAvailable,
                StartTime = exception.StartTime != null ? TimeSpan.Parse(exception.StartTime) : null,
                EndTime = exception.EndTime != null ? TimeSpan.Parse(exception.EndTime) : null,
                Reason = exception.Reason,
                CreatedAt = DateTime.UtcNow
            };

            await _supabase.From<TutorAvailabilityExceptionEntity>().Insert(entity);

            return ServiceResult.SuccessResult();
        }
        catch (Exception ex)
        {
            return ServiceResult.FailureResult($"Failed to add availability exception: {ex.Message}");
        }
    }

    public async Task<ServiceResult> DeleteAvailabilityAsync(Guid availabilityId)
    {
        try
        {
            await _supabase
                .From<TutorAvailabilityEntity>()
                .Where(x => x.AvailabilityId == availabilityId)
                .Delete();

            return ServiceResult.SuccessResult();
        }
        catch (Exception ex)
        {
            return ServiceResult.FailureResult($"Failed to delete availability: {ex.Message}");
        }
    }

    public async Task<ServiceResult> SaveStudentAvailabilityAsync(StudentAvailabilityDto studentAvailability, int studentId, Guid? bookingRequestId = null)
    {
        try
        {
            // Delete existing availability for this student and booking request
            var query = _supabase
                .From<StudentAvailabilityEntity>()
                .Where(x => x.StudentId == studentId);

            if (bookingRequestId.HasValue)
            {
                query = query.Where(x => x.BookingRequestId == bookingRequestId);
            }

            await query.Delete();

            // Insert new availability records
            var entities = new List<StudentAvailabilityEntity>();

            foreach (var day in studentAvailability.PreferredDays)
            {
                foreach (var timeOfDay in studentAvailability.PreferredTimes)
                {
                    var entity = new StudentAvailabilityEntity
                    {
                        AvailabilityId = Guid.NewGuid(),
                        StudentId = studentId,
                        BookingRequestId = bookingRequestId,
                        DayOfWeek = day,
                        TimeOfDay = timeOfDay,
                        SpecificHours = studentAvailability.SpecificHours?.ContainsKey(day) == true
                            ? System.Text.Json.JsonSerializer.Serialize(studentAvailability.SpecificHours[day])
                            : null,
                        CreatedAt = DateTime.UtcNow
                    };
                    entities.Add(entity);
                }
            }

            if (entities.Any())
            {
                await _supabase.From<StudentAvailabilityEntity>().Insert(entities);
            }

            return ServiceResult.SuccessResult();
        }
        catch (Exception ex)
        {
            return ServiceResult.FailureResult($"Failed to save student availability: {ex.Message}");
        }
    }

    public async Task<List<AvailabilityExceptionDto>> GetTutorExceptionsAsync(int tutorId, DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            var query = _supabase
                .From<TutorAvailabilityExceptionEntity>()
                .Where(x => x.TutorId == tutorId);

            if (startDate.HasValue)
            {
                query = query.Where(x => x.ExceptionDate >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(x => x.ExceptionDate <= endDate.Value);
            }

            var response = await query.Get();
            var entities = response.Models;

            return entities.Select(MapExceptionToDto).ToList();
        }
        catch (Exception ex)
        {
            return new List<AvailabilityExceptionDto>();
        }
    }

    public async Task<ServiceResult> DeleteAvailabilityExceptionAsync(Guid exceptionId)
    {
        try
        {
            await _supabase
                .From<TutorAvailabilityExceptionEntity>()
                .Where(x => x.ExceptionId == exceptionId)
                .Delete();

            return ServiceResult.SuccessResult();
        }
        catch (Exception ex)
        {
            return ServiceResult.FailureResult($"Failed to delete availability exception: {ex.Message}");
        }
    }

    private static AvailabilityBlockDto MapToDto(TutorAvailabilityEntity entity)
    {
        return new AvailabilityBlockDto
        {
            AvailabilityId = entity.AvailabilityId,
            TutorId = entity.TutorId,
            ModuleId = entity.ModuleId,
            DayOfWeek = entity.DayOfWeek,
            StartTime = entity.StartTime.ToString(@"hh\:mm"),
            EndTime = entity.EndTime.ToString(@"hh\:mm"),
            IsRecurring = entity.IsRecurring,
            EffectiveFrom = entity.EffectiveFrom,
            EffectiveUntil = entity.EffectiveUntil,
            Timezone = entity.Timezone
        };
    }

    private static AvailabilityExceptionDto MapExceptionToDto(TutorAvailabilityExceptionEntity entity)
    {
        return new AvailabilityExceptionDto
        {
            ExceptionId = entity.ExceptionId,
            TutorId = entity.TutorId,
            AvailabilityId = entity.AvailabilityId,
            ExceptionDate = entity.ExceptionDate,
            IsAvailable = entity.IsAvailable,
            StartTime = entity.StartTime?.ToString(@"hh\:mm"),
            EndTime = entity.EndTime?.ToString(@"hh\:mm"),
            Reason = entity.Reason
        };
    }
}
