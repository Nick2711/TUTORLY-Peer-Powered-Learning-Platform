using Supabase;
using Supabase.Postgrest.Models;
using Supabase.Postgrest;
using Tutorly.Server.DatabaseModels;
using Tutorly.Shared;
using System.Globalization;

namespace Tutorly.Server.Services;

public class BookingService : IBookingService
{
    private readonly Supabase.Client _supabase;
    private readonly IAvailabilityService _availabilityService;
    private readonly IAuditLogService _auditLogService;
    private const int MINIMUM_ADVANCE_DAYS = 7;

    public BookingService(Supabase.Client supabase, IAvailabilityService availabilityService, IAuditLogService auditLogService)
    {
        _supabase = supabase;
        _availabilityService = availabilityService;
        _auditLogService = auditLogService;
    }

    public async Task<List<BookableSlotDto>> PreviewBookableSlotsAsync(int studentId, int tutorId, int moduleId, DateTime startDate, DateTime endDate, StudentAvailabilityDto studentAvailability)
    {
        try
        {
            // Enforce 7-day minimum advance booking
            var minimumBookingDate = DateTime.UtcNow.Date.AddDays(MINIMUM_ADVANCE_DAYS);
            if (endDate < minimumBookingDate)
            {
                endDate = minimumBookingDate.AddDays(7); // Show 7 days starting from minimum
            }
            if (startDate < minimumBookingDate)
            {
                startDate = minimumBookingDate;
            }

            Console.WriteLine($"DEBUG: Enforced 7-day minimum - StartDate: {startDate:yyyy-MM-dd}, EndDate: {endDate:yyyy-MM-dd}");

            // Get tutor preferences for this module
            var preferences = await GetModuleTutorPreferencesAsync(tutorId, moduleId);
            var prefs = preferences.Data ?? new ModuleTutorPreferencesDto();

            // Debug: Check what's in the database
            Console.WriteLine($"DEBUG: Checking database for TutorId={tutorId}, ModuleId={moduleId}");

            // Check ModuleTutorEntity to see what tutor IDs are linked to this module
            var moduleTutors = await _supabase.From<ModuleTutorEntity>()
                .Where(x => x.ModuleId == moduleId)
                .Get();
            Console.WriteLine($"DEBUG: Found {moduleTutors.Models.Count} module-tutor relationships for module {moduleId}");
            foreach (var mt in moduleTutors.Models)
            {
                Console.WriteLine($"DEBUG: Module {mt.ModuleId} - Tutor {mt.TutorId}");
            }

            // Check TutorProfileEntity to see what tutor IDs exist
            var tutorProfiles = await _supabase.From<TutorProfileEntity>().Get();
            Console.WriteLine($"DEBUG: Found {tutorProfiles.Models.Count} tutor profiles in database");
            foreach (var tp in tutorProfiles.Models)
            {
                Console.WriteLine($"DEBUG: TutorProfile - TutorId: {tp.TutorId}, UserId: {tp.UserId}, FullName: {tp.FullName}");
            }

            // Check all availability records
            var allAvailability = await _supabase.From<TutorAvailabilityEntity>().Get();
            Console.WriteLine($"DEBUG: Found {allAvailability.Models.Count} total availability records in database");
            foreach (var avail in allAvailability.Models)
            {
                Console.WriteLine($"DEBUG: Tutor {avail.TutorId} - Module {avail.ModuleId} - Day {avail.DayOfWeek} - {avail.StartTime}-{avail.EndTime}");
            }

            // Check specific tutor
            var tutorAvailabilityEntities = await _supabase.From<TutorAvailabilityEntity>()
                .Where(x => x.TutorId == tutorId)
                .Get();
            Console.WriteLine($"DEBUG: Found {tutorAvailabilityEntities.Models.Count} availability records for tutor {tutorId}");

            // Get tutor availability via service
            var tutorAvailability = await _availabilityService.GetTutorAvailabilityAsync(tutorId, moduleId);
            var tutorExceptions = await _availabilityService.GetTutorExceptionsAsync(tutorId, startDate, endDate);

            // Get existing sessions for conflict checking
            var existingSessions = await GetExistingSessionsAsync(tutorId, studentId, startDate, endDate);

            // Debug logging
            Console.WriteLine($"DEBUG: StudentId={studentId}, TutorId={tutorId}, ModuleId={moduleId}");
            Console.WriteLine($"DEBUG: Student preferred days: [{string.Join(",", studentAvailability.PreferredDays)}]");
            Console.WriteLine($"DEBUG: Student preferred times: [{string.Join(",", studentAvailability.PreferredTimes)}]");
            Console.WriteLine($"DEBUG: Tutor availability blocks: {tutorAvailability.Count}");
            foreach (var avail in tutorAvailability)
            {
                Console.WriteLine($"DEBUG: Tutor availability - Day {avail.DayOfWeek}, {avail.StartTime}-{avail.EndTime}");
            }

            var slots = new List<BookableSlotDto>();
            var currentDate = startDate.Date;

            while (currentDate <= endDate.Date)
            {
                var dayOfWeek = (int)currentDate.DayOfWeek;

                // Check if student is available this day
                if (!studentAvailability.PreferredDays.Contains(dayOfWeek))
                {
                    currentDate = currentDate.AddDays(1);
                    continue;
                }

                // Get tutor availability for this day
                var dayAvailability = tutorAvailability
                    .Where(a => a.DayOfWeek == dayOfWeek &&
                               a.EffectiveFrom <= currentDate &&
                               (a.EffectiveUntil == null || a.EffectiveUntil >= currentDate))
                    .ToList();

                Console.WriteLine($"DEBUG: {currentDate:yyyy-MM-dd} (Day {dayOfWeek}) - Found {dayAvailability.Count} tutor availability blocks");

                // Check for exceptions on this date
                var exception = tutorExceptions.FirstOrDefault(e => e.ExceptionDate.Date == currentDate.Date);
                if (exception != null && !exception.IsAvailable)
                {
                    Console.WriteLine($"DEBUG: {currentDate:yyyy-MM-dd} - Blocked by exception");
                    currentDate = currentDate.AddDays(1);
                    continue;
                }

                // Generate slots for this day
                foreach (var availability in dayAvailability)
                {
                    var startTime = TimeSpan.Parse(availability.StartTime);
                    var endTime = TimeSpan.Parse(availability.EndTime);

                    Console.WriteLine($"DEBUG: Processing availability block {startTime}-{endTime}");

                    // Apply exception times if present
                    if (exception != null && exception.IsAvailable && exception.StartTime != null && exception.EndTime != null)
                    {
                        startTime = TimeSpan.Parse(exception.StartTime);
                        endTime = TimeSpan.Parse(exception.EndTime);
                    }

                    var slotStart = currentDate.Add(startTime);
                    var slotEnd = currentDate.Add(endTime);

                    // Generate slots within this availability window
                    var currentSlotStart = slotStart;
                    while (currentSlotStart.AddMinutes(prefs.SlotLengthMinutes) <= slotEnd)
                    {
                        var currentSlotEnd = currentSlotStart.AddMinutes(prefs.SlotLengthMinutes);

                        // Check if this slot is available
                        var isAvailable = await IsSlotAvailableAsync(
                            currentSlotStart, currentSlotEnd,
                            studentAvailability, dayOfWeek,
                            existingSessions, prefs, currentDate);

                        Console.WriteLine($"DEBUG: Slot {currentSlotStart:HH:mm}-{currentSlotEnd:HH:mm} - Available: {isAvailable.IsAvailable}, Reason: {isAvailable.Reason}");

                        var slot = new BookableSlotDto
                        {
                            SlotStart = currentSlotStart,
                            SlotEnd = currentSlotEnd,
                            IsAvailable = isAvailable.IsAvailable,
                            UnavailableReason = isAvailable.Reason,
                            DisplayTime = FormatDisplayTime(currentSlotStart)
                        };

                        slots.Add(slot);
                        currentSlotStart = currentSlotStart.AddMinutes(prefs.SlotLengthMinutes);
                    }
                }

                currentDate = currentDate.AddDays(1);
            }

            Console.WriteLine($"DEBUG: Generated {slots.Count} total slots, {slots.Count(s => s.IsAvailable)} available");
            return slots;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Exception in PreviewBookableSlotsAsync: {ex.Message}");
            return new List<BookableSlotDto>();
        }
    }

    public async Task<ServiceResult<BookingRequestDto>> CreateBookingRequestAsync(BookingRequestDto request)
    {
        try
        {
            Console.WriteLine($"DEBUG: CreateBookingRequestAsync - StudentId={request.StudentId}, TutorId={request.TutorId}, ModuleId={request.ModuleId}");
            Console.WriteLine($"DEBUG: CreateBookingRequestAsync - RequestedSlots count: {request.RequestedSlots?.Count ?? 0}");

            var entity = new BookingRequestEntity
            {
                RequestId = request.RequestId ?? Guid.NewGuid(),
                StudentId = request.StudentId,
                TutorId = request.TutorId,
                ModuleId = request.ModuleId,
                Status = "Pending",
                StudentAvailabilityJson = System.Text.Json.JsonSerializer.Serialize(request.StudentPreferencesJson),
                RequestedSlotsJson = System.Text.Json.JsonSerializer.Serialize(request.RequestedSlots),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7) // 7 days to respond
            };

            Console.WriteLine($"DEBUG: CreateBookingRequestAsync - Inserting booking request entity with ID: {entity.RequestId}");
            await _supabase.From<BookingRequestEntity>().Insert(entity);
            Console.WriteLine($"DEBUG: CreateBookingRequestAsync - Successfully inserted booking request");

            // Save student availability
            var studentAvailability = System.Text.Json.JsonSerializer.Deserialize<StudentAvailabilityDto>(request.StudentPreferencesJson);
            if (studentAvailability != null)
            {
                Console.WriteLine($"DEBUG: CreateBookingRequestAsync - Saving student availability");
                await _availabilityService.SaveStudentAvailabilityAsync(studentAvailability, request.StudentId, entity.RequestId);
                Console.WriteLine($"DEBUG: CreateBookingRequestAsync - Successfully saved student availability");
            }

            return ServiceResult<BookingRequestDto>.SuccessResult(request);
        }
        catch (Exception ex)
        {
            return ServiceResult<BookingRequestDto>.FailureResult($"Failed to create booking request: {ex.Message}");
        }
    }

    public async Task<ServiceResult<List<SessionDto>>> ConfirmBookingAsync(Guid requestId, List<DateTime> approvedSlots, int tutorId)
    {
        try
        {
            // Get the booking request
            var request = await _supabase
                .From<BookingRequestEntity>()
                .Where(x => x.RequestId == requestId && x.TutorId == tutorId)
                .Single();

            if (request == null)
            {
                return ServiceResult<List<SessionDto>>.FailureResult("Booking request not found");
            }

            // Re-validate slots are still available (race condition check)
            var studentAvailability = System.Text.Json.JsonSerializer.Deserialize<StudentAvailabilityDto>(request.StudentAvailabilityJson);
            if (studentAvailability == null)
            {
                return ServiceResult<List<SessionDto>>.FailureResult("Invalid student availability data");
            }

            var preferences = await GetModuleTutorPreferencesAsync(tutorId, request.ModuleId);
            var prefs = preferences.Data ?? new ModuleTutorPreferencesDto();

            var sessions = new List<SessionDto>();

            foreach (var slotStart in approvedSlots)
            {
                var slotEnd = slotStart.AddMinutes(prefs.SlotLengthMinutes);

                // Double-check slot is still available
                var existingSessions = await GetExistingSessionsAsync(tutorId, request.StudentId, slotStart.Date, slotEnd.Date);
                var isAvailable = await IsSlotAvailableAsync(
                    slotStart, slotEnd, studentAvailability, (int)slotStart.DayOfWeek,
                    existingSessions, prefs, slotStart.Date);

                if (!isAvailable.IsAvailable)
                {
                    return ServiceResult<List<SessionDto>>.FailureResult($"Slot {slotStart:yyyy-MM-dd HH:mm} is no longer available: {isAvailable.Reason}");
                }

                // Create session
                var sessionId = Guid.NewGuid();
                var sessionEntity = new SessionEntity
                {
                    SessionId = sessionId,
                    BookingRequestId = requestId,
                    StudentId = request.StudentId,
                    TutorId = tutorId,
                    ModuleId = request.ModuleId,
                    ScheduledStart = slotStart.ToUniversalTime(),
                    ScheduledEnd = slotEnd.ToUniversalTime(),
                    Status = "Confirmed",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _supabase.From<SessionEntity>().Insert(sessionEntity);

                // Study room will be created when the session actually starts
                // This is handled by the SessionActivationBackgroundService

                sessions.Add(new SessionDto
                {
                    SessionId = sessionId,
                    StudentId = request.StudentId,
                    TutorId = tutorId,
                    ModuleId = request.ModuleId,
                    ScheduledStart = slotStart,
                    ScheduledEnd = slotEnd,
                    StudyRoomId = null, // Will be created when session starts
                    Status = "Confirmed"
                });
            }

            // Update booking request status
            request.Status = "Approved";
            request.RespondedAt = DateTime.UtcNow;
            await _supabase.From<BookingRequestEntity>().Where(x => x.RequestId == requestId).Update(request);

            return ServiceResult<List<SessionDto>>.SuccessResult(sessions);
        }
        catch (Exception ex)
        {
            return ServiceResult<List<SessionDto>>.FailureResult($"Failed to confirm booking: {ex.Message}");
        }
    }

    public async Task<ServiceResult> CancelSessionAsync(Guid sessionId, int userId, string reason)
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

            // Check if user can cancel (is student or tutor)
            if (session.StudentId != userId && session.TutorId != userId)
            {
                return ServiceResult.FailureResult("You don't have permission to cancel this session");
            }

            // Check cancellation policy
            var preferences = await GetModuleTutorPreferencesAsync(session.TutorId, session.ModuleId);
            var prefs = preferences.Data ?? new ModuleTutorPreferencesDto();

            var timeUntilSession = session.ScheduledStart - DateTime.UtcNow;
            if (timeUntilSession.TotalHours < prefs.CancellationCutoffHours)
            {
                return ServiceResult.FailureResult($"Cannot cancel session less than {prefs.CancellationCutoffHours} hours before start time");
            }

            // Update session
            session.Status = "Cancelled";
            session.CancellationReason = reason;
            session.CancelledBy = userId;
            session.CancelledAt = DateTime.UtcNow;
            session.UpdatedAt = DateTime.UtcNow;

            await _supabase.From<SessionEntity>().Where(x => x.SessionId == sessionId).Update(session);

            // Update study room if exists
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
            return ServiceResult.FailureResult($"Failed to cancel session: {ex.Message}");
        }
    }

    public async Task<List<BookingRequestDto>> GetPendingRequestsAsync(int tutorId)
    {
        try
        {
            var response = await _supabase
                .From<BookingRequestEntity>()
                .Where(x => x.TutorId == tutorId && x.Status == "Pending")
                .Get();

            return response.Models.OrderByDescending(x => x.CreatedAt).Select(MapBookingRequestToDto).ToList();
        }
        catch (Exception ex)
        {
            return new List<BookingRequestDto>();
        }
    }

    public async Task<List<SessionDto>> GetUserSessionsAsync(int userId, DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            Console.WriteLine($"DEBUG: GetUserSessionsAsync - UserId={userId}, StartDate={startDate:O}, EndDate={endDate:O}");

            var query = _supabase
                .From<SessionEntity>()
                .Where(x => x.StudentId == userId || x.TutorId == userId);

            if (startDate.HasValue)
            {
                query = query.Where(x => x.ScheduledStart >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(x => x.ScheduledStart <= endDate.Value);
            }

            var response = await query.Get();
            Console.WriteLine($"DEBUG: GetUserSessionsAsync - Found {response.Models.Count} session entities");

            var sessions = new List<SessionDto>();
            foreach (var entity in response.Models.OrderBy(x => x.ScheduledStart))
            {
                var sessionDto = await MapSessionToDtoAsync(entity);
                sessions.Add(sessionDto);
            }
            Console.WriteLine($"DEBUG: GetUserSessionsAsync - Mapped to {sessions.Count} session DTOs");

            return sessions;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: GetUserSessionsAsync - Exception: {ex.Message}");
            return new List<SessionDto>();
        }
    }

    public async Task<List<CalendarEventDto>> GetCalendarEventsAsync(int userId, DateTime startDate, DateTime endDate)
    {
        try
        {
            Console.WriteLine($"DEBUG: GetCalendarEventsAsync - UserId={userId}, StartDate={startDate:O}, EndDate={endDate:O}");

            var sessions = await GetUserSessionsAsync(userId, startDate, endDate);
            Console.WriteLine($"DEBUG: GetCalendarEventsAsync - Found {sessions.Count} sessions");

            var events = sessions.Select(session => new CalendarEventDto
            {
                SessionId = session.SessionId,
                Title = $"{session.ModuleCode} - {session.ModuleName}",
                Start = session.ScheduledStart,
                End = session.ScheduledEnd,
                Type = "TutorSession",
                StudyRoomId = session.StudyRoomId,
                ParticipantName = session.StudentId == userId ? session.TutorName : session.StudentName,
                Status = session.Status
            }).ToList();

            Console.WriteLine($"DEBUG: GetCalendarEventsAsync - Returning {events.Count} calendar events");
            return events;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: GetCalendarEventsAsync - Exception: {ex.Message}");
            return new List<CalendarEventDto>();
        }
    }

    public async Task<List<UpcomingSessionDto>> GetUpcomingSessionsAsync(int userId, int limit = 3)
    {
        try
        {
            Console.WriteLine($"DEBUG: GetUpcomingSessionsAsync - UserId={userId}, Limit={limit}");

            var now = DateTime.UtcNow;
            var sessions = await GetUserSessionsAsync(userId, now, null);

            // Filter for sessions that can be joined (confirmed or in progress) and sort by start time
            var upcomingSessions = sessions
                .Where(s => (s.Status == "Confirmed" || s.Status == "InProgress") && s.ScheduledStart > now.AddHours(-2))
                .OrderBy(s => s.ScheduledStart)
                .Take(limit)
                .Select(session =>
                {
                    var timeUntilSession = session.ScheduledStart - DateTime.UtcNow;
                    var hoursUntilSession = timeUntilSession.TotalHours;

                    return new UpcomingSessionDto
                    {
                        SessionId = session.SessionId,
                        Title = $"{session.ModuleCode} - {session.ModuleName}",
                        ModuleName = session.ModuleName,
                        ModuleCode = session.ModuleCode,
                        ParticipantName = session.StudentId == userId ? session.TutorName : session.StudentName,
                        ScheduledStart = session.ScheduledStart,
                        ScheduledEnd = session.ScheduledEnd,
                        Status = session.Status,
                        StudyRoomId = session.StudyRoomId,
                        TimeDisplay = FormatTimeDisplay(session.ScheduledStart, session.ScheduledEnd),
                        DateDisplay = FormatDateDisplay(session.ScheduledStart),
                        IsToday = session.ScheduledStart.Date == DateTime.Today,
                        IsTomorrow = session.ScheduledStart.Date == DateTime.Today.AddDays(1),

                        // Action button states
                        CanJoin = (session.Status == "InProgress") || (hoursUntilSession <= 0.25 && hoursUntilSession >= -2), // In progress or 15 min before to 2 hours after
                        CanReschedule = hoursUntilSession > 24, // More than 24 hours away
                        CanCancel = hoursUntilSession > 12, // More than 12 hours away (cancellation cutoff)
                        StatusColor = session.Status.ToLower() switch
                        {
                            "confirmed" => "success",
                            "inprogress" => "primary",
                            "pending" => "warning",
                            "cancelled" => "danger",
                            "completed" => "info",
                            _ => "default"
                        }
                    };
                })
                .ToList();

            Console.WriteLine($"DEBUG: GetUpcomingSessionsAsync - Returning {upcomingSessions.Count} upcoming sessions");
            return upcomingSessions;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: GetUpcomingSessionsAsync - Exception: {ex.Message}");
            return new List<UpcomingSessionDto>();
        }
    }

    private static string FormatTimeDisplay(DateTime start, DateTime end)
    {
        var duration = end - start;
        var durationText = duration.TotalMinutes == 60 ? "1 hour" : $"{duration.TotalMinutes} mins";

        if (start.Date == DateTime.Today)
        {
            return $"Today at {start:HH:mm} ({durationText})";
        }
        else if (start.Date == DateTime.Today.AddDays(1))
        {
            return $"Tomorrow at {start:HH:mm} ({durationText})";
        }
        else
        {
            return $"{start:MMM d} at {start:HH:mm} ({durationText})";
        }
    }

    private static string FormatDateDisplay(DateTime date)
    {
        if (date.Date == DateTime.Today)
        {
            return "Today";
        }
        else if (date.Date == DateTime.Today.AddDays(1))
        {
            return "Tomorrow";
        }
        else
        {
            return date.ToString("MMM d, yyyy");
        }
    }

    public async Task<ServiceResult<ModuleTutorPreferencesDto>> GetModuleTutorPreferencesAsync(int tutorId, int moduleId)
    {
        try
        {
            var preferences = await _supabase
                .From<ModuleTutorPreferencesEntity>()
                .Where(x => x.TutorId == tutorId && x.ModuleId == moduleId)
                .Single();

            if (preferences == null)
            {
                // Return default preferences
                return ServiceResult<ModuleTutorPreferencesDto>.SuccessResult(new ModuleTutorPreferencesDto
                {
                    TutorId = tutorId,
                    ModuleId = moduleId
                });
            }

            var dto = new ModuleTutorPreferencesDto
            {
                PreferenceId = preferences.PreferenceId,
                TutorId = preferences.TutorId,
                ModuleId = preferences.ModuleId,
                SlotLengthMinutes = preferences.SlotLengthMinutes,
                BufferMinutes = preferences.BufferMinutes,
                LeadTimeHours = preferences.LeadTimeHours,
                BookingWindowDays = preferences.BookingWindowDays,
                MaxSessionsPerDay = preferences.MaxSessionsPerDay,
                CancellationCutoffHours = preferences.CancellationCutoffHours
            };

            return ServiceResult<ModuleTutorPreferencesDto>.SuccessResult(dto);
        }
        catch (Exception ex)
        {
            return ServiceResult<ModuleTutorPreferencesDto>.FailureResult($"Failed to get preferences: {ex.Message}");
        }
    }

    public async Task<ServiceResult> SetModuleTutorPreferencesAsync(ModuleTutorPreferencesDto preferences)
    {
        try
        {
            var entity = new ModuleTutorPreferencesEntity
            {
                PreferenceId = preferences.PreferenceId ?? Guid.NewGuid(),
                TutorId = preferences.TutorId,
                ModuleId = preferences.ModuleId,
                SlotLengthMinutes = preferences.SlotLengthMinutes,
                BufferMinutes = preferences.BufferMinutes,
                LeadTimeHours = preferences.LeadTimeHours,
                BookingWindowDays = preferences.BookingWindowDays,
                MaxSessionsPerDay = preferences.MaxSessionsPerDay,
                CancellationCutoffHours = preferences.CancellationCutoffHours,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _supabase.From<ModuleTutorPreferencesEntity>().Upsert(entity);

            return ServiceResult.SuccessResult();
        }
        catch (Exception ex)
        {
            return ServiceResult.FailureResult($"Failed to set preferences: {ex.Message}");
        }
    }

    private async Task<List<SessionEntity>> GetExistingSessionsAsync(int tutorId, int studentId, DateTime startDate, DateTime endDate)
    {
        try
        {
            var sessions = await _supabase
                .From<SessionEntity>()
                .Where(x => (x.TutorId == tutorId || x.StudentId == studentId) &&
                           x.Status != "Cancelled" &&
                           x.ScheduledStart >= startDate &&
                           x.ScheduledEnd <= endDate)
                .Get();

            return sessions.Models.ToList();
        }
        catch (Exception ex)
        {
            return new List<SessionEntity>();
        }
    }

    private async Task<(bool IsAvailable, string? Reason)> IsSlotAvailableAsync(
        DateTime slotStart, DateTime slotEnd,
        StudentAvailabilityDto studentAvailability, int dayOfWeek,
        List<SessionEntity> existingSessions, ModuleTutorPreferencesDto prefs, DateTime date)
    {
        // Check 7-day minimum advance booking
        var daysUntilSlot = (slotStart.Date - DateTime.UtcNow.Date).Days;
        if (daysUntilSlot < MINIMUM_ADVANCE_DAYS)
        {
            return (false, BookingErrorCodes.MINIMUM_ADVANCE_NOT_MET);
        }

        // Check lead time
        var timeUntilSlot = slotStart - DateTime.UtcNow;
        if (timeUntilSlot.TotalHours < prefs.LeadTimeHours)
        {
            return (false, BookingErrorCodes.LEAD_TIME_NOT_MET);
        }

        // Check booking window
        if (daysUntilSlot > prefs.BookingWindowDays)
        {
            return (false, BookingErrorCodes.BOOKING_WINDOW_EXCEEDED);
        }

        // Check daily session limit
        var sessionsOnDate = existingSessions.Count(s => s.ScheduledStart.Date == date);
        if (sessionsOnDate >= prefs.MaxSessionsPerDay)
        {
            return (false, BookingErrorCodes.DAILY_LIMIT_REACHED);
        }

        // Check for conflicts with existing sessions (with buffer)
        var bufferStart = slotStart.AddMinutes(-prefs.BufferMinutes);
        var bufferEnd = slotEnd.AddMinutes(prefs.BufferMinutes);

        var hasConflict = existingSessions.Any(s =>
            (s.ScheduledStart < bufferEnd && s.ScheduledEnd > bufferStart));

        if (hasConflict)
        {
            return (false, BookingErrorCodes.BUFFER_CONFLICT);
        }

        // Check student time preferences
        var timeOfDay = GetTimeOfDay(slotStart.TimeOfDay);
        if (!studentAvailability.PreferredTimes.Contains(timeOfDay))
        {
            return (false, BookingErrorCodes.STUDENT_PREFERENCE_MISMATCH);
        }

        return (true, null);
    }

    private string GetTimeOfDay(TimeSpan time)
    {
        if (time.Hours < 12) return "Morning";
        if (time.Hours < 17) return "Afternoon";
        return "Evening";
    }

    private string FormatDisplayTime(DateTime dateTime)
    {
        return dateTime.ToString("ddd, MMM dd - h:mm tt", CultureInfo.InvariantCulture);
    }

    // Study rooms will be created when sessions actually start, not when they're scheduled
    // This is handled by the SessionActivationBackgroundService

    private static BookingRequestDto MapBookingRequestToDto(BookingRequestEntity entity)
    {
        return new BookingRequestDto
        {
            RequestId = entity.RequestId,
            StudentId = entity.StudentId,
            TutorId = entity.TutorId,
            ModuleId = entity.ModuleId,
            Status = entity.Status,
            StudentPreferencesJson = entity.StudentAvailabilityJson,
            RequestedSlots = System.Text.Json.JsonSerializer.Deserialize<List<DateTime>>(entity.RequestedSlotsJson) ?? new(),
            CreatedAt = entity.CreatedAt,
            ExpiresAt = entity.ExpiresAt,
            RespondedAt = entity.RespondedAt
        };
    }

    private async Task<SessionDto> MapSessionToDtoAsync(SessionEntity entity)
    {
        var sessionDto = new SessionDto
        {
            SessionId = entity.SessionId,
            StudentId = entity.StudentId,
            TutorId = entity.TutorId,
            ModuleId = entity.ModuleId,
            ScheduledStart = entity.ScheduledStart,
            ScheduledEnd = entity.ScheduledEnd,
            StudyRoomId = entity.StudyRoomId,
            Status = entity.Status,
            CancellationReason = entity.CancellationReason,
            CancelledAt = entity.CancelledAt
        };

        try
        {
            // Get student name
            var studentProfile = await _supabase
                .From<StudentProfileEntity>()
                .Where(x => x.StudentId == entity.StudentId)
                .Single();
            sessionDto.StudentName = studentProfile?.FullName ?? "Unknown Student";

            // Get tutor name
            var tutorProfile = await _supabase
                .From<TutorProfileEntity>()
                .Where(x => x.TutorId == entity.TutorId)
                .Single();
            sessionDto.TutorName = tutorProfile?.FullName ?? "Unknown Tutor";

            // Get module information
            var module = await _supabase
                .From<ModuleEntity>()
                .Where(x => x.ModuleId == entity.ModuleId)
                .Single();
            sessionDto.ModuleCode = module?.ModuleCode ?? "Unknown";
            sessionDto.ModuleName = module?.ModuleName ?? "Unknown Module";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: MapSessionToDtoAsync - Error loading related data: {ex.Message}");
            // Set defaults if lookup fails
            sessionDto.StudentName = "Unknown Student";
            sessionDto.TutorName = "Unknown Tutor";
            sessionDto.ModuleCode = "Unknown";
            sessionDto.ModuleName = "Unknown Module";
        }

        return sessionDto;
    }

    public async Task<List<BookingRequestDto>> GetPendingBookingRequestsAsync(int tutorId)
    {
        try
        {
            var response = await _supabase
                .From<BookingRequestEntity>()
                .Where(x => x.TutorId == tutorId && x.Status == "Pending")
                .Get();

            var entities = response.Models.OrderByDescending(x => x.CreatedAt).ToList();

            var result = new List<BookingRequestDto>();
            foreach (var entity in entities)
            {
                Console.WriteLine($"DEBUG: Processing booking request - StudentId: {entity.StudentId}, ModuleId: {entity.ModuleId}");

                // Get student name from student profile
                StudentProfileEntity? studentProfile = null;
                try
                {
                    Console.WriteLine($"DEBUG: Looking for student profile with StudentId: {entity.StudentId}");

                    // First try the specific query
                    studentProfile = await _supabase
                        .From<StudentProfileEntity>()
                        .Filter("student_id", Supabase.Postgrest.Constants.Operator.Equals, entity.StudentId)
                        .Single();

                    Console.WriteLine($"DEBUG: Student profile found - FullName: '{studentProfile?.FullName}', UserId: '{studentProfile?.UserId}'");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"DEBUG: Error getting student profile for StudentId {entity.StudentId}: {ex.Message}");

                    // Try alternative query to see all profiles
                    try
                    {
                        var studentProfiles = await _supabase
                            .From<StudentProfileEntity>()
                            .Get();

                        Console.WriteLine($"DEBUG: Found {studentProfiles.Models.Count} total student profiles");
                        foreach (var profile in studentProfiles.Models)
                        {
                            Console.WriteLine($"DEBUG: Profile - StudentId: {profile.StudentId}, FullName: '{profile.FullName}', UserId: '{profile.UserId}'");
                        }

                        // Try to find a match manually
                        var matchingProfile = studentProfiles.Models.FirstOrDefault(p => p.StudentId == entity.StudentId);
                        if (matchingProfile != null)
                        {
                            Console.WriteLine($"DEBUG: Found matching profile manually: {matchingProfile.FullName}");
                            studentProfile = matchingProfile;
                        }
                        else
                        {
                            Console.WriteLine($"DEBUG: No matching profile found for StudentId {entity.StudentId}");
                        }
                    }
                    catch (Exception ex2)
                    {
                        Console.WriteLine($"DEBUG: Error getting all student profiles: {ex2.Message}");
                    }
                }

                // Get module name from module
                var module = await _supabase
                    .From<ModuleEntity>()
                    .Filter("module_id", Supabase.Postgrest.Constants.Operator.Equals, entity.ModuleId)
                    .Single();

                Console.WriteLine($"DEBUG: Module - Name: '{module?.ModuleName}'");

                var dto = new BookingRequestDto
                {
                    RequestId = entity.RequestId,
                    StudentId = entity.StudentId,
                    StudentName = studentProfile?.FullName ?? "Unknown Student",
                    TutorId = entity.TutorId,
                    ModuleId = entity.ModuleId,
                    ModuleName = module?.ModuleName ?? "Unknown Module",
                    Status = entity.Status,
                    CreatedAt = entity.CreatedAt,
                    RequestedSlots = entity.RequestedSlotsJson != null
                        ? System.Text.Json.JsonSerializer.Deserialize<List<DateTime>>(entity.RequestedSlotsJson)
                        : new List<DateTime>(),
                    StudentPreferencesJson = entity.StudentAvailabilityJson
                };

                result.Add(dto);
            }

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: GetPendingBookingRequestsAsync - Exception: {ex.Message}");
            return new List<BookingRequestDto>();
        }
    }

    public async Task<ServiceResult> ConfirmBookingRequestAsync(Guid requestId)
    {
        try
        {
            Console.WriteLine($"DEBUG: ConfirmBookingRequestAsync - RequestId: {requestId}");
            Console.WriteLine($"DEBUG: RequestId type: {requestId.GetType()}");
            Console.WriteLine($"DEBUG: RequestId as string: '{requestId.ToString()}'");

            // First, let's try to get all booking requests to see what's in the database
            try
            {
                var allRequests = await _supabase
                    .From<BookingRequestEntity>()
                    .Get();

                Console.WriteLine($"DEBUG: Found {allRequests.Models.Count} total booking requests in database");
                foreach (var req in allRequests.Models.Take(5)) // Show first 5
                {
                    Console.WriteLine($"DEBUG: Request - ID: {req.RequestId}, StudentId: {req.StudentId}, TutorId: {req.TutorId}, Status: {req.Status}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: Error getting all requests: {ex.Message}");
            }

            // Get the booking request
            var requestResponse = await _supabase
                .From<BookingRequestEntity>()
                .Where(x => x.RequestId == requestId)
                .Single();

            if (requestResponse == null)
            {
                return ServiceResult.FailureResult("Booking request not found");
            }

            // Update request status to Approved
            await _supabase
                .From<BookingRequestEntity>()
                .Where(x => x.RequestId == requestId)
                .Set(x => x.Status, "Approved")
                .Set(x => x.RespondedAt, DateTime.UtcNow)
                .Update();

            // Get the actual user ID for the creator (tutor) - outside the loop
            var tutorProfile = await _supabase
                .From<TutorProfileEntity>()
                .Where(x => x.TutorId == requestResponse.TutorId)
                .Single();

            Console.WriteLine($"DEBUG: Tutor profile - UserId: {tutorProfile?.UserId}");
            Console.WriteLine($"DEBUG: Tutor profile - UserId type: {tutorProfile?.UserId?.GetType()}");

            // Convert string UserId to Guid for CreatorUserId
            Guid creatorUserId;
            if (!string.IsNullOrEmpty(tutorProfile?.UserId) && Guid.TryParse(tutorProfile.UserId, out creatorUserId))
            {
                Console.WriteLine($"DEBUG: Successfully parsed UserId to Guid: {creatorUserId}");
            }
            else
            {
                Console.WriteLine($"DEBUG: Failed to parse UserId, using new Guid");
                creatorUserId = Guid.NewGuid();
            }

            // Create sessions for each requested slot
            var requestedSlots = requestResponse.RequestedSlotsJson != null
                ? System.Text.Json.JsonSerializer.Deserialize<List<DateTime>>(requestResponse.RequestedSlotsJson)
                : new List<DateTime>();

            foreach (var slotStart in requestedSlots)
            {
                var slotEnd = slotStart.AddHours(1); // Default 1-hour sessions

                Console.WriteLine($"DEBUG: Processing slot - Start: {slotStart}, End: {slotEnd}");
                Console.WriteLine($"DEBUG: RequestResponse - StudentId: {requestResponse.StudentId}, TutorId: {requestResponse.TutorId}, ModuleId: {requestResponse.ModuleId}");

                // Create session (without study room - will be created when session starts)
                var session = new SessionEntity
                {
                    SessionId = Guid.NewGuid(),
                    BookingRequestId = requestId,
                    StudentId = requestResponse.StudentId,
                    TutorId = requestResponse.TutorId,
                    ModuleId = requestResponse.ModuleId,
                    StudyRoomId = null, // Will be created when session starts
                    ScheduledStart = slotStart,
                    ScheduledEnd = slotEnd,
                    Status = "Confirmed",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                Console.WriteLine($"DEBUG: About to insert session - SessionId: {session.SessionId}, StudentId: {session.StudentId}, TutorId: {session.TutorId}");
                await _supabase.From<SessionEntity>().Insert(session);
                Console.WriteLine($"DEBUG: Session inserted successfully");

                // Create calendar events for both student and tutor
                await CreateCalendarEventsAsync(session, requestResponse);
            }

            // Auto-enroll student in module if not already enrolled
            await EnsureStudentEnrollmentAsync(requestResponse.StudentId, requestResponse.ModuleId);

            // Log successful booking
            await _auditLogService.LogBookingAttemptAsync(
                Guid.Parse(tutorProfile?.UserId ?? Guid.NewGuid().ToString()),
                requestResponse.TutorId,
                requestResponse.ModuleId,
                requestedSlots.FirstOrDefault(),
                true,
                "Booking confirmed successfully"
            );

            return ServiceResult.SuccessResult();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: ConfirmBookingRequestAsync - Exception: {ex.Message}");

            // Log booking failure
            await _auditLogService.LogBookingFailureAsync(
                null, // We don't have user ID in catch block
                "BOOKING_CONFIRMATION_ERROR",
                $"Error confirming booking request: {ex.Message}",
                new { RequestId = requestId, Exception = ex.Message }
            );

            return ServiceResult.FailureResult($"Error confirming booking request: {ex.Message}");
        }
    }

    public async Task<ServiceResult> RejectBookingRequestAsync(Guid requestId)
    {
        try
        {
            Console.WriteLine($"DEBUG: RejectBookingRequestAsync - RequestId: {requestId}");

            // Get the booking request first to log it
            var requestResponse = await _supabase
                .From<BookingRequestEntity>()
                .Where(x => x.RequestId == requestId)
                .Single();

            // Update request status to Rejected
            await _supabase
                .From<BookingRequestEntity>()
                .Where(x => x.RequestId == requestId)
                .Set(x => x.Status, "Rejected")
                .Set(x => x.RespondedAt, DateTime.UtcNow)
                .Update();

            // Log booking rejection
            if (requestResponse != null)
            {
                await _auditLogService.LogEventAsync(
                    "BookingRejected",
                    null, // We don't have user ID easily available here
                    "BookingRequest",
                    requestId.ToString(),
                    new
                    {
                        StudentId = requestResponse.StudentId,
                        TutorId = requestResponse.TutorId,
                        ModuleId = requestResponse.ModuleId
                    }
                );
            }

            return ServiceResult.SuccessResult();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: RejectBookingRequestAsync - Exception: {ex.Message}");

            // Log rejection failure
            await _auditLogService.LogBookingFailureAsync(
                null,
                "BOOKING_REJECTION_ERROR",
                $"Error rejecting booking request: {ex.Message}",
                new { RequestId = requestId, Exception = ex.Message }
            );

            return ServiceResult.FailureResult($"Error rejecting booking request: {ex.Message}");
        }
    }

    public async Task<object> GetDatabaseStatusAsync()
    {
        try
        {
            // Get tutor availability records
            var tutorAvailability = await _supabase.From<TutorAvailabilityEntity>().Get();

            // Get booking requests
            var bookingRequests = await _supabase.From<BookingRequestEntity>().Get();

            // Get sessions
            var sessions = await _supabase.From<SessionEntity>().Get();

            // Get module tutor relationships
            var moduleTutors = await _supabase.From<ModuleTutorEntity>().Get();

            // Get modules
            var modules = await _supabase.From<ModuleEntity>().Get();

            // Get tutor profiles
            var tutorProfiles = await _supabase.From<TutorProfileEntity>().Get();

            // Get student profiles
            var studentProfiles = await _supabase.From<StudentProfileEntity>().Get();

            return new
            {
                Timestamp = DateTime.UtcNow,
                Counts = new
                {
                    TutorAvailability = tutorAvailability.Models.Count,
                    BookingRequests = bookingRequests.Models.Count,
                    Sessions = sessions.Models.Count,
                    ModuleTutors = moduleTutors.Models.Count,
                    Modules = modules.Models.Count,
                    TutorProfiles = tutorProfiles.Models.Count,
                    StudentProfiles = studentProfiles.Models.Count
                },
                RecentActivity = new
                {
                    RecentBookingRequests = bookingRequests.Models
                        .OrderByDescending(x => x.CreatedAt)
                        .Take(5)
                        .Select(x => new
                        {
                            x.RequestId,
                            x.StudentId,
                            x.TutorId,
                            x.ModuleId,
                            x.Status,
                            x.CreatedAt
                        }),
                    RecentSessions = sessions.Models
                        .OrderByDescending(x => x.CreatedAt)
                        .Take(5)
                        .Select(x => new
                        {
                            x.SessionId,
                            x.StudentId,
                            x.TutorId,
                            x.ModuleId,
                            x.Status,
                            x.ScheduledStart,
                            x.CreatedAt
                        })
                },
                TutorAvailabilityDetails = tutorAvailability.Models
                    .GroupBy(x => x.TutorId)
                    .Select(g => new
                    {
                        TutorId = g.Key,
                        ModuleCount = g.Count(x => x.ModuleId.HasValue),
                        GeneralCount = g.Count(x => !x.ModuleId.HasValue),
                        Days = g.Select(x => x.DayOfWeek).Distinct().OrderBy(x => x).ToList()
                    })
                    .ToList(),
                BookingRequestStatusBreakdown = bookingRequests.Models
                    .GroupBy(x => x.Status)
                    .Select(g => new
                    {
                        Status = g.Key,
                        Count = g.Count()
                    })
                    .ToList(),
                SessionStatusBreakdown = sessions.Models
                    .GroupBy(x => x.Status)
                    .Select(g => new
                    {
                        Status = g.Key,
                        Count = g.Count()
                    })
                    .ToList()
            };
        }
        catch (Exception ex)
        {
            return new
            {
                Error = ex.Message,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    private async Task CreateCalendarEventsAsync(SessionEntity session, BookingRequestEntity request)
    {
        try
        {
            Console.WriteLine($"DEBUG: Creating calendar events for session {session.SessionId}");
            Console.WriteLine($"DEBUG: Session StudentId: {session.StudentId}, TutorId: {session.TutorId}");

            // First, let's see what student profiles exist
            try
            {
                var allStudentProfiles = await _supabase
                    .From<StudentProfileEntity>()
                    .Get();

                Console.WriteLine($"DEBUG: Found {allStudentProfiles.Models.Count} total student profiles");
                foreach (var profile in allStudentProfiles.Models)
                {
                    Console.WriteLine($"DEBUG: StudentProfile - StudentId: {profile.StudentId}, FullName: '{profile.FullName}', UserId: '{profile.UserId}'");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: Error getting all student profiles: {ex.Message}");
            }

            // Get student and tutor names for the calendar events
            StudentProfileEntity? studentProfile = null;
            try
            {
                studentProfile = await _supabase
                    .From<StudentProfileEntity>()
                    .Where(x => x.StudentId == session.StudentId)
                    .Single();

                Console.WriteLine($"DEBUG: Student profile found - FullName: '{studentProfile?.FullName}', UserId: '{studentProfile?.UserId}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: Error getting student profile for StudentId {session.StudentId}: {ex.Message}");

                // Try to find manually from the list we got earlier
                try
                {
                    var allStudentProfiles = await _supabase
                        .From<StudentProfileEntity>()
                        .Get();

                    studentProfile = allStudentProfiles.Models.FirstOrDefault(p => p.StudentId == session.StudentId);
                    if (studentProfile != null)
                    {
                        Console.WriteLine($"DEBUG: Found student profile manually: {studentProfile.FullName}");
                    }
                }
                catch (Exception ex2)
                {
                    Console.WriteLine($"DEBUG: Error in manual student profile lookup: {ex2.Message}");
                }
            }

            var tutorProfile = await _supabase
                .From<TutorProfileEntity>()
                .Where(x => x.TutorId == session.TutorId)
                .Single();

            var module = await _supabase
                .From<ModuleEntity>()
                .Where(x => x.ModuleId == session.ModuleId)
                .Single();

            // Create calendar event for student
            var studentEvent = new CalendarEventDto
            {
                SessionId = session.SessionId,
                Title = $"Tutoring Session - {module?.ModuleName ?? "Unknown Module"}",
                Start = session.ScheduledStart,
                End = session.ScheduledEnd,
                Type = "TutorSession",
                StudyRoomId = session.StudyRoomId,
                ParticipantName = tutorProfile?.FullName ?? "Unknown Tutor",
                Status = session.Status
            };

            // Create calendar event for tutor
            var tutorEvent = new CalendarEventDto
            {
                SessionId = session.SessionId,
                Title = $"Tutoring Session - {module?.ModuleName ?? "Unknown Module"}",
                Start = session.ScheduledStart,
                End = session.ScheduledEnd,
                Type = "TutorSession",
                StudyRoomId = session.StudyRoomId,
                ParticipantName = studentProfile?.FullName ?? "Unknown Student",
                Status = session.Status
            };

            // Note: Calendar events would typically be stored in a calendar_events table
            // For now, we'll just log them. In a full implementation, you'd insert them into the database
            Console.WriteLine($"DEBUG: Student calendar event - {studentEvent.Title} with {studentEvent.ParticipantName}");
            Console.WriteLine($"DEBUG: Tutor calendar event - {tutorEvent.Title} with {tutorEvent.ParticipantName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Error creating calendar events: {ex.Message}");
        }
    }

    private async Task EnsureStudentEnrollmentAsync(int studentId, int moduleId)
    {
        try
        {
            Console.WriteLine($"DEBUG: Checking enrollment - StudentId={studentId}, ModuleId={moduleId}");

            // Check if student is already enrolled
            var existingEnrollment = await _supabase
                .From<ModuleStudentEntity>()
                .Where(x => x.StudentId == studentId && x.ModuleId == moduleId)
                .Get();

            if (existingEnrollment?.Models?.Any() != true)
            {
                Console.WriteLine($"DEBUG: Student not enrolled, creating enrollment record");

                // Create enrollment
                var enrollment = new ModuleStudentEntity
                {
                    StudentId = studentId,
                    ModuleId = moduleId
                };

                await _supabase.From<ModuleStudentEntity>().Insert(enrollment);
                Console.WriteLine($"DEBUG: Successfully enrolled student {studentId} in module {moduleId}");
            }
            else
            {
                Console.WriteLine($"DEBUG: Student already enrolled in module");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Error ensuring enrollment: {ex.Message}");
            // Don't throw - enrollment is a nice-to-have, shouldn't break session creation
        }
    }

    // =============================================================================
    // SLOT LOCKING METHODS
    // =============================================================================

    private async Task<bool> TryLockSlotAsync(int tutorId, DateTime slotStart, DateTime slotEnd, int studentId)
    {
        try
        {
            var lockExpiry = DateTime.UtcNow.AddMinutes(10); // 10-minute lock

            // Clean up expired locks first
            await CleanupExpiredLocksAsync();

            // Check if slot is already locked by someone else
            var existingLock = await _supabase
                .From<SlotLockEntity>()
                .Where(x => x.TutorId == tutorId &&
                            x.SlotStart == slotStart &&
                            x.ExpiresAt > DateTime.UtcNow)
                .Get();

            if (existingLock?.Models?.Any() == true)
            {
                var slotLock = existingLock.Models.First();
                // If locked by another student, deny the lock
                if (slotLock.LockedByStudentId != studentId)
                {
                    Console.WriteLine($"DEBUG: Slot already locked by student {slotLock.LockedByStudentId}, denying lock for student {studentId}");
                    return false;
                }

                // If locked by same student, extend the lock
                Console.WriteLine($"DEBUG: Extending existing lock for student {studentId}");
                slotLock.ExpiresAt = lockExpiry;
                await _supabase.From<SlotLockEntity>()
                    .Where(x => x.LockId == slotLock.LockId)
                    .Set(x => x.ExpiresAt, lockExpiry)
                    .Update();
                return true;
            }

            // Create new lock
            var newLock = new SlotLockEntity
            {
                LockId = Guid.NewGuid(),
                TutorId = tutorId,
                SlotStart = slotStart,
                SlotEnd = slotEnd,
                LockedByStudentId = studentId,
                LockedAt = DateTime.UtcNow,
                ExpiresAt = lockExpiry
            };

            await _supabase.From<SlotLockEntity>().Insert(newLock);
            Console.WriteLine($"DEBUG: Created new slot lock for student {studentId}, expires at {lockExpiry}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Failed to lock slot: {ex.Message}");
            return false;
        }
    }

    private async Task CleanupExpiredLocksAsync()
    {
        try
        {
            var expiredLocks = await _supabase
                .From<SlotLockEntity>()
                .Where(x => x.ExpiresAt < DateTime.UtcNow)
                .Get();

            if (expiredLocks?.Models?.Any() == true)
            {
                foreach (var slotLock in expiredLocks.Models)
                {
                    await _supabase.From<SlotLockEntity>()
                        .Where(x => x.LockId == slotLock.LockId)
                        .Delete();
                }

                Console.WriteLine($"DEBUG: Cleaned up {expiredLocks.Models.Count} expired slot locks");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Failed to cleanup expired locks: {ex.Message}");
        }
    }

    private async Task ReleaseLockAsync(int tutorId, DateTime slotStart, int studentId)
    {
        try
        {
            var locks = await _supabase
                .From<SlotLockEntity>()
                .Where(x => x.TutorId == tutorId &&
                            x.SlotStart == slotStart &&
                            x.LockedByStudentId == studentId)
                .Get();

            if (locks?.Models?.Any() == true)
            {
                foreach (var slotLock in locks.Models)
                {
                    await _supabase.From<SlotLockEntity>()
                        .Where(x => x.LockId == slotLock.LockId)
                        .Delete();
                }

                Console.WriteLine($"DEBUG: Released {locks.Models.Count} slot locks for student {studentId}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Failed to release lock: {ex.Message}");
        }
    }

    private async Task ReleaseAllStudentLocksAsync(int studentId)
    {
        try
        {
            var locks = await _supabase
                .From<SlotLockEntity>()
                .Where(x => x.LockedByStudentId == studentId)
                .Get();

            if (locks?.Models?.Any() == true)
            {
                foreach (var slotLock in locks.Models)
                {
                    await _supabase.From<SlotLockEntity>()
                        .Where(x => x.LockId == slotLock.LockId)
                        .Delete();
                }

                Console.WriteLine($"DEBUG: Released all {locks.Models.Count} locks for student {studentId}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Failed to release student locks: {ex.Message}");
        }
    }

    public async Task<TutorAnalyticsDto> GetTutorAnalyticsAsync(int tutorId, int days = 30)
    {
        try
        {
            Console.WriteLine($"DEBUG: GetTutorAnalyticsAsync - TutorId={tutorId}, Days={days}");

            var startDate = DateTime.UtcNow.AddDays(-days);
            var endDate = DateTime.UtcNow;

            // Get all sessions for this tutor in the specified period
            var sessionsResponse = await _supabase
                .From<SessionEntity>()
                .Where(x => x.TutorId == tutorId)
                .Where(x => x.ScheduledStart >= startDate)
                .Where(x => x.ScheduledStart <= endDate)
                .Get();

            var sessions = sessionsResponse.Models.ToList();
            Console.WriteLine($"DEBUG: GetTutorAnalyticsAsync - Found {sessions.Count} sessions");

            // Get ratings for this tutor within the same date range
            var ratingsResponse = await _supabase
                .From<TutorRatingEntity>()
                .Where(r => r.TutorId == tutorId)
                .Where(r => r.CreatedAt >= startDate)
                .Where(r => r.CreatedAt <= endDate)
                .Get();

            var ratings = ratingsResponse.Models.ToList();
            Console.WriteLine($"DEBUG: GetTutorAnalyticsAsync - Found {ratings.Count} ratings within date range");

            // Get verified responses for this tutor within the same date range
            var verifiedResponsesResponse = await _supabase
                .From<ForumResponseEntity>()
                .Where(r => r.VerifiedByTutorId == tutorId)
                .Where(r => r.VerifiedAt >= startDate)
                .Where(r => r.VerifiedAt <= endDate)
                .Get();

            var verifiedResponses = verifiedResponsesResponse.Models.ToList();
            Console.WriteLine($"DEBUG: GetTutorAnalyticsAsync - Found {verifiedResponses.Count} verified responses within date range");

            // Calculate metrics
            var totalSessions = sessions.Count;
            var uniqueStudents = sessions.Select(s => s.StudentId).Distinct().Count();
            var totalMinutes = sessions.Sum(s => (int)(s.ScheduledEnd - s.ScheduledStart).TotalMinutes);
            var totalHours = totalMinutes / 60.0;

            // Calculate average rating
            var averageRating = ratings.Any() ? ratings.Average(r => r.Rating) : 0.0;

            // Calculate no-show rate
            // For now, we'll consider sessions with status "Cancelled" as no-shows
            // In the future, we might want to track actual no-shows differently
            var noShowSessions = sessions.Count(s => s.Status == "Cancelled");
            var noShowRate = totalSessions > 0 ? (double)noShowSessions / totalSessions : 0.0;

            // Get recent sessions (last 10)
            var recentSessions = sessions
                .OrderByDescending(s => s.ScheduledStart)
                .Take(10)
                .Select(async s =>
                {
                    // Get module name
                    var moduleResponse = await _supabase
                        .From<ModuleEntity>()
                        .Where(m => m.ModuleId == s.ModuleId)
                        .Single();

                    // Get student name
                    var studentResponse = await _supabase
                        .From<StudentProfileEntity>()
                        .Where(st => st.StudentId == s.StudentId)
                        .Single();

                    // Get rating for this session (match by tutor, student, and module)
                    var sessionRating = ratings.FirstOrDefault(r =>
                        r.TutorId == s.TutorId &&
                        r.StudentId == s.StudentId &&
                        r.ModuleId == s.ModuleId);

                    return new TutorAnalyticsSessionDto
                    {
                        SessionId = s.SessionId,
                        ScheduledStart = s.ScheduledStart,
                        ScheduledEnd = s.ScheduledEnd,
                        ModuleName = moduleResponse?.ModuleName ?? "Unknown",
                        StudentName = studentResponse?.FullName ?? $"Student {s.StudentId}",
                        DurationMinutes = (int)(s.ScheduledEnd - s.ScheduledStart).TotalMinutes,
                        Rating = sessionRating?.Rating,
                        Status = s.Status,
                        IsNoShow = s.Status == "Cancelled"
                    };
                })
                .ToList();

            var recentSessionsList = new List<TutorAnalyticsSessionDto>();
            foreach (var task in recentSessions)
            {
                recentSessionsList.Add(await task);
            }

            // Get top students by hours
            var studentStats = sessions
                .GroupBy(s => s.StudentId)
                .Select(async g =>
                {
                    var studentResponse = await _supabase
                        .From<StudentProfileEntity>()
                        .Where(st => st.StudentId == g.Key)
                        .Single();

                    var studentRatings = ratings.Where(r => r.StudentId == g.Key).ToList();
                    var avgRating = studentRatings.Any() ? studentRatings.Average(r => r.Rating) : (double?)null;

                    return new TutorAnalyticsStudentDto
                    {
                        StudentId = g.Key,
                        StudentName = studentResponse?.FullName ?? $"Student {g.Key}",
                        SessionCount = g.Count(),
                        TotalHours = g.Sum(s => (s.ScheduledEnd - s.ScheduledStart).TotalHours),
                        AverageRating = avgRating
                    };
                })
                .ToList();

            var topStudentsList = new List<TutorAnalyticsStudentDto>();
            foreach (var task in studentStats)
            {
                topStudentsList.Add(await task);
            }

            var topStudents = topStudentsList
                .OrderByDescending(s => s.TotalHours)
                .Take(6)
                .ToList();

            var analytics = new TutorAnalyticsDto
            {
                TotalSessions = totalSessions,
                UniqueStudents = uniqueStudents,
                TotalHours = totalHours,
                AverageRating = averageRating,
                NoShowRate = noShowRate,
                VerifiedResponses = verifiedResponses.Count,
                DaysAnalyzed = days,
                RecentSessions = recentSessionsList,
                TopStudents = topStudents
            };

            Console.WriteLine($"DEBUG: GetTutorAnalyticsAsync - Returning analytics: {totalSessions} sessions, {uniqueStudents} students, {totalHours:F1} hours, {averageRating:F1} rating, {noShowRate:P1} no-show rate");

            return analytics;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: GetTutorAnalyticsAsync - Exception: {ex.Message}");
            return new TutorAnalyticsDto();
        }
    }
}
