using Microsoft.AspNetCore.Mvc;
using Tutorly.Server.Services;
using Tutorly.Shared;
using Tutorly.Server.DatabaseModels;
using Supabase;
using System.Text;

namespace Tutorly.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BookingController : ControllerBase
{
    private readonly IBookingService _bookingService;
    private readonly Supabase.Client _supabase;
    private readonly IPdfExportService _pdfExportService;

    public BookingController(IBookingService bookingService, Supabase.Client supabase, IPdfExportService pdfExportService)
    {
        _bookingService = bookingService;
        _supabase = supabase;
        _pdfExportService = pdfExportService;
    }

    [HttpPost("preview-slots")]
    public async Task<ActionResult<List<BookableSlotDto>>> PreviewBookableSlots([FromBody] PreviewSlotsRequest request)
    {
        try
        {
            var slots = await _bookingService.PreviewBookableSlotsAsync(
                request.StudentId,
                request.TutorId,
                request.ModuleId,
                request.StartDate,
                request.EndDate,
                request.StudentAvailability);

            return Ok(slots);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error previewing bookable slots: {ex.Message}");
        }
    }

    [HttpPost("request")]
    public async Task<ActionResult<ServiceResult<BookingRequestDto>>> CreateBookingRequest([FromBody] BookingRequestDto request)
    {
        try
        {
            var result = await _bookingService.CreateBookingRequestAsync(request);

            if (result.Success)
                return Ok(result);

            return BadRequest(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error creating booking request: {ex.Message}");
        }
    }

    [HttpPost("confirm")]
    public async Task<ActionResult<ServiceResult<List<SessionDto>>>> ConfirmBooking([FromBody] ConfirmBookingRequest request)
    {
        try
        {
            var result = await _bookingService.ConfirmBookingAsync(request.RequestId, request.ApprovedSlots, request.TutorId);

            if (result.Success)
                return Ok(result);

            return BadRequest(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error confirming booking: {ex.Message}");
        }
    }

    [HttpPost("cancel")]
    public async Task<ActionResult<ServiceResult>> CancelSession([FromBody] CancelSessionRequest request)
    {
        try
        {
            var result = await _bookingService.CancelSessionAsync(request.SessionId, request.UserId, request.Reason);

            if (result.Success)
                return Ok(result);

            return BadRequest(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error cancelling session: {ex.Message}");
        }
    }

    [HttpGet("requests/pending/{tutorId}")]
    public async Task<ActionResult<List<BookingRequestDto>>> GetPendingRequests(int tutorId)
    {
        try
        {
            var requests = await _bookingService.GetPendingRequestsAsync(tutorId);
            return Ok(requests);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving pending requests: {ex.Message}");
        }
    }

    [HttpGet("sessions/{userId}")]
    public async Task<ActionResult<List<SessionDto>>> GetUserSessions(int userId, [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
    {
        try
        {
            Console.WriteLine($"DEBUG: GetUserSessions Controller - UserId={userId}, StartDate={startDate:O}, EndDate={endDate:O}");
            var sessions = await _bookingService.GetUserSessionsAsync(userId, startDate, endDate);
            Console.WriteLine($"DEBUG: GetUserSessions Controller - Returning {sessions.Count} sessions");
            return Ok(sessions);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: GetUserSessions Controller - Exception: {ex.Message}");
            return StatusCode(500, $"Error retrieving user sessions: {ex.Message}");
        }
    }

    [HttpGet("calendar/{userId}")]
    public async Task<ActionResult<List<CalendarEventDto>>> GetCalendarEvents(int userId, [FromQuery] string startDate, [FromQuery] string endDate)
    {
        try
        {
            Console.WriteLine($"DEBUG: GetCalendarEvents Controller - UserId={userId}, StartDate={startDate}, EndDate={endDate}");

            // First, let's check what sessions exist in the database
            var allSessions = await _supabase.From<SessionEntity>().Get();
            Console.WriteLine($"DEBUG: Total sessions in database: {allSessions.Models.Count}");
            foreach (var session in allSessions.Models)
            {
                Console.WriteLine($"DEBUG: Session - StudentId: {session.StudentId}, TutorId: {session.TutorId}, Status: {session.Status}, Start: {session.ScheduledStart}");
            }

            // Parse the date strings manually to handle timezone issues
            // Fix timezone format: replace space with + in timezone offset
            var fixedStartDate = startDate.Replace(" 02:00", "+02:00").Replace(" 01:00", "+01:00").Replace(" 00:00", "+00:00");
            var fixedEndDate = endDate.Replace(" 02:00", "+02:00").Replace(" 01:00", "+01:00").Replace(" 00:00", "+00:00");

            if (!DateTime.TryParse(fixedStartDate, out DateTime parsedStartDate))
            {
                Console.WriteLine($"DEBUG: Failed to parse startDate: {startDate} (fixed: {fixedStartDate})");
                return BadRequest($"Invalid startDate format: {startDate}");
            }

            if (!DateTime.TryParse(fixedEndDate, out DateTime parsedEndDate))
            {
                Console.WriteLine($"DEBUG: Failed to parse endDate: {endDate} (fixed: {fixedEndDate})");
                return BadRequest($"Invalid endDate format: {endDate}");
            }

            Console.WriteLine($"DEBUG: Parsed dates - StartDate={parsedStartDate:O}, EndDate={parsedEndDate:O}");

            var events = await _bookingService.GetCalendarEventsAsync(userId, parsedStartDate, parsedEndDate);
            Console.WriteLine($"DEBUG: GetCalendarEvents Controller - Returning {events.Count} events");

            return Ok(events);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: GetCalendarEvents Controller - Exception: {ex.Message}");
            return StatusCode(500, $"Error retrieving calendar events: {ex.Message}");
        }
    }

    /// <summary>
    /// Tutor-initiated session creation (bypasses booking request flow)
    /// Creates a Confirmed session which will appear in calendars and upcoming lists.
    /// </summary>
    [HttpPost("create-session")]
    public async Task<ActionResult<ServiceResult<SessionDto>>> CreateSession([FromBody] CreateSessionRequest req)
    {
        try
        {
            // Validate minimal required fields
            if (req.TutorId <= 0 || req.StudentId <= 0 || req.ModuleId <= 0)
            {
                return BadRequest(ServiceResult<SessionDto>.FailureResult("Missing required fields"));
            }

            // Persist in UTC, accept local time from client
            var startUtc = DateTime.SpecifyKind(req.ScheduledStart, DateTimeKind.Local).ToUniversalTime();
            var endUtc = startUtc.AddMinutes(req.DurationMinutes <= 0 ? 60 : req.DurationMinutes);

            var sessionId = Guid.NewGuid();

            // Insert session row
            var sessionEntity = new SessionEntity
            {
                SessionId = sessionId,
                BookingRequestId = null,
                StudentId = req.StudentId,
                TutorId = req.TutorId,
                ModuleId = req.ModuleId,
                StudyRoomId = null, // Study room is created on session activation
                ScheduledStart = startUtc,
                ScheduledEnd = endUtc,
                Status = "Confirmed",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _supabase.From<SessionEntity>().Insert(sessionEntity);

            // Return a lightweight DTO with local times for client display
            var dto = new SessionDto
            {
                SessionId = sessionId,
                StudentId = req.StudentId,
                TutorId = req.TutorId,
                ModuleId = req.ModuleId,
                ScheduledStart = req.ScheduledStart,
                ScheduledEnd = req.ScheduledStart.AddMinutes(req.DurationMinutes <= 0 ? 60 : req.DurationMinutes),
                Status = "Confirmed"
            };

            return Ok(ServiceResult<SessionDto>.SuccessResult(dto));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ServiceResult<SessionDto>.FailureResult($"Error creating session: {ex.Message}"));
        }
    }



    [HttpGet("upcoming-sessions/{userId}")]
    public async Task<ActionResult<List<UpcomingSessionDto>>> GetUpcomingSessions(int userId, [FromQuery] int limit = 3)
    {
        try
        {
            Console.WriteLine($"DEBUG: GetUpcomingSessions - UserId={userId}, Limit={limit}");

            var upcomingSessions = await _bookingService.GetUpcomingSessionsAsync(userId, limit);
            Console.WriteLine($"DEBUG: GetUpcomingSessions - Found {upcomingSessions.Count} upcoming sessions");

            return Ok(upcomingSessions);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: GetUpcomingSessions - Exception: {ex.Message}");
            return StatusCode(500, $"Error retrieving upcoming sessions: {ex.Message}");
        }
    }

    /// <summary>
    /// Get tutor analytics including sessions count, students count, hours, rating, and no-show rate
    /// </summary>
    [HttpGet("analytics/{tutorId}")]
    public async Task<ActionResult<TutorAnalyticsDto>> GetTutorAnalytics(int tutorId, [FromQuery] int days = 30)
    {
        try
        {
            Console.WriteLine($"DEBUG: GetTutorAnalytics - TutorId={tutorId}, Days={days}");

            var analytics = await _bookingService.GetTutorAnalyticsAsync(tutorId, days);
            Console.WriteLine($"DEBUG: GetTutorAnalytics - Returning analytics for tutor {tutorId}");

            return Ok(analytics);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: GetTutorAnalytics - Exception: {ex.Message}");
            return StatusCode(500, $"Error retrieving tutor analytics: {ex.Message}");
        }
    }

    /// <summary>
    /// Create or retrieve a private study room for a confirmed session, and return its RoomId
    /// </summary>
    [HttpPost("sessions/{sessionId}/start-room")]
    public async Task<ActionResult<object>> StartOrGetStudyRoom(Guid sessionId)
    {
        try
        {
            // Load session
            var sessionResp = await _supabase
                .From<SessionEntity>()
                .Where(s => s.SessionId == sessionId)
                .Get();

            var session = sessionResp.Models.FirstOrDefault();
            if (session == null)
            {
                return NotFound($"Session {sessionId} not found");
            }

            // If a room already exists, return it
            if (session.StudyRoomId.HasValue)
            {
                return Ok(new { roomId = session.StudyRoomId.Value });
            }

            // Create a new private study room for this session
            var room = new StudyRoomEntity
            {
                RoomId = Guid.NewGuid(),
                RoomName = $"Session {session.SessionId.ToString().Substring(0, 8)}",
                Description = "Private session room",
                CreatorUserId = Guid.NewGuid(), // Will be replaced by actual caller user id in future
                CreatedAt = DateTime.UtcNow,
                ScheduledStartTime = session.ScheduledStart,
                ScheduledEndTime = session.ScheduledEnd,
                RoomType = "Standalone", // Allow joining without room code for call rooms
                Privacy = "PrivateInviteOnly",
                ModuleId = null,
                MaxParticipants = 10,
                Status = "Active",
                RoomCode = null,
                UpdatedAt = DateTime.UtcNow
            };

            // Insert room
            await _supabase.From<StudyRoomEntity>().Insert(room);

            // Link room to session
            session.StudyRoomId = room.RoomId;
            session.Status = session.Status == "Confirmed" ? "InProgress" : session.Status;
            session.UpdatedAt = DateTime.UtcNow;
            await _supabase.From<SessionEntity>()
                .Where(s => s.SessionId == session.SessionId)
                .Update(session);

            return Ok(new { roomId = room.RoomId });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error starting study room: {ex.Message}");
        }
    }

    [HttpGet("debug/sessions")]
    public async Task<ActionResult> DebugSessions()
    {
        try
        {
            var sessions = await _supabase
                .From<SessionEntity>()
                .Get();

            var debugInfo = new
            {
                TotalSessions = sessions.Models.Count,
                Sessions = sessions.Models.Select(s => new
                {
                    s.SessionId,
                    s.StudentId,
                    s.TutorId,
                    s.ModuleId,
                    s.ScheduledStart,
                    s.ScheduledEnd,
                    s.Status,
                    s.StudyRoomId
                }).ToList()
            };

            return Ok(debugInfo);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving sessions: {ex.Message}");
        }
    }

    [HttpGet("preferences/{tutorId}/{moduleId}")]
    public async Task<ActionResult<ServiceResult<ModuleTutorPreferencesDto>>> GetModuleTutorPreferences(int tutorId, int moduleId)
    {
        try
        {
            var result = await _bookingService.GetModuleTutorPreferencesAsync(tutorId, moduleId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving preferences: {ex.Message}");
        }
    }

    [HttpPost("preferences")]
    public async Task<ActionResult<ServiceResult>> SetModuleTutorPreferences([FromBody] ModuleTutorPreferencesDto preferences)
    {
        try
        {
            var result = await _bookingService.SetModuleTutorPreferencesAsync(preferences);

            if (result.Success)
                return Ok(result);

            return BadRequest(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error setting preferences: {ex.Message}");
        }
    }

    [HttpGet("pending-requests/{tutorId}")]
    public async Task<ActionResult<List<BookingRequestDto>>> GetPendingBookingRequests(int tutorId)
    {
        try
        {
            var result = await _bookingService.GetPendingBookingRequestsAsync(tutorId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving pending requests: {ex.Message}");
        }
    }

    [HttpPost("confirm/{requestId}")]
    public async Task<ActionResult<ServiceResult>> ConfirmBookingRequest(Guid requestId)
    {
        try
        {
            var result = await _bookingService.ConfirmBookingRequestAsync(requestId);
            if (result.Success)
            {
                return Ok(result);
            }
            return BadRequest(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error confirming booking request: {ex.Message}");
        }
    }

    [HttpPost("reject/{requestId}")]
    public async Task<ActionResult<ServiceResult>> RejectBookingRequest(Guid requestId)
    {
        try
        {
            var result = await _bookingService.RejectBookingRequestAsync(requestId);
            if (result.Success)
            {
                return Ok(result);
            }
            return BadRequest(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error rejecting booking request: {ex.Message}");
        }
    }

    [HttpGet("debug/database-status")]
    public async Task<ActionResult<object>> GetDatabaseStatus()
    {
        try
        {
            var result = await _bookingService.GetDatabaseStatusAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving database status: {ex.Message}");
        }
    }

    [HttpGet("analytics/{tutorId}/export-pdf")]
    public async Task<ActionResult> ExportAnalyticsPdf(int tutorId, [FromQuery] int days = 30)
    {
        try
        {
            Console.WriteLine($"DEBUG: ExportAnalyticsPdf - TutorId={tutorId}, Days={days}");

            // Get analytics data
            Console.WriteLine("DEBUG: Getting analytics data...");
            var analytics = await _bookingService.GetTutorAnalyticsAsync(tutorId, days);
            Console.WriteLine($"DEBUG: Got analytics data - {analytics.TotalSessions} sessions");

            // Get tutor name
            Console.WriteLine("DEBUG: Getting tutor name...");
            var tutorResponse = await _supabase
                .From<TutorProfileEntity>()
                .Where(t => t.TutorId == tutorId)
                .Get();

            var tutorName = tutorResponse.Models.FirstOrDefault()?.FullName ?? $"Tutor {tutorId}";
            Console.WriteLine($"DEBUG: Tutor name: {tutorName}");

            // Generate PDF
            Console.WriteLine("DEBUG: Generating PDF...");
            var pdfBytes = await _pdfExportService.GenerateAnalyticsPdfAsync(analytics, tutorName, days);
            Console.WriteLine($"DEBUG: PDF generated - {pdfBytes.Length} bytes");

            // Return PDF file
            var fileName = $"TutorAnalytics_{tutorName.Replace(" ", "_")}_{days}days_{DateTime.Now:yyyyMMdd}.pdf";
            Console.WriteLine($"DEBUG: Returning PDF file: {fileName}");
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: ExportAnalyticsPdf - Exception: {ex.Message}");
            Console.WriteLine($"DEBUG: ExportAnalyticsPdf - StackTrace: {ex.StackTrace}");
            return StatusCode(500, $"Error generating PDF: {ex.Message}");
        }
    }

    [HttpGet("analytics/{tutorId}/export-csv")]
    public async Task<ActionResult> ExportAnalyticsCsv(int tutorId, [FromQuery] int days = 30)
    {
        try
        {
            Console.WriteLine($"DEBUG: ExportAnalyticsCsv - TutorId={tutorId}, Days={days}");

            // Get analytics data
            Console.WriteLine("DEBUG: Getting analytics data for CSV...");
            var analytics = await _bookingService.GetTutorAnalyticsAsync(tutorId, days);
            Console.WriteLine($"DEBUG: Got analytics data - {analytics.TotalSessions} sessions");

            // Get tutor name
            Console.WriteLine("DEBUG: Getting tutor name for CSV...");
            var tutorResponse = await _supabase
                .From<TutorProfileEntity>()
                .Where(t => t.TutorId == tutorId)
                .Get();

            var tutorName = tutorResponse.Models.FirstOrDefault()?.FullName ?? $"Tutor {tutorId}";
            Console.WriteLine($"DEBUG: Tutor name: {tutorName}");

            // Generate CSV content
            Console.WriteLine("DEBUG: Generating CSV content...");
            var csvContent = GenerateAnalyticsCsv(analytics, tutorName, days);
            Console.WriteLine($"DEBUG: CSV generated - {csvContent.Length} characters");

            // Return CSV file
            var fileName = $"TutorAnalytics_{tutorName.Replace(" ", "_")}_{days}days_{DateTime.Now:yyyyMMdd}.csv";
            Console.WriteLine($"DEBUG: Returning CSV file: {fileName}");
            return File(Encoding.UTF8.GetBytes(csvContent), "text/csv", fileName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: ExportAnalyticsCsv - Exception: {ex.Message}");
            Console.WriteLine($"DEBUG: ExportAnalyticsCsv - StackTrace: {ex.StackTrace}");
            return StatusCode(500, $"Error generating CSV: {ex.Message}");
        }
    }

    private string GenerateAnalyticsCsv(TutorAnalyticsDto analytics, string tutorName, int days)
    {
        var csv = new StringBuilder();

        // Header information
        csv.AppendLine($"Tutor Analytics Report");
        csv.AppendLine($"Tutor: {tutorName}");
        csv.AppendLine($"Report Period: Last {days} days");
        csv.AppendLine($"Generated: {DateTime.Now:MMMM dd, yyyy 'at' HH:mm}");
        csv.AppendLine();

        // Key Metrics
        csv.AppendLine("KEY PERFORMANCE METRICS");
        csv.AppendLine("Metric,Value");
        csv.AppendLine($"Total Sessions,{analytics.TotalSessions}");
        csv.AppendLine($"Unique Students,{analytics.UniqueStudents}");
        csv.AppendLine($"Total Hours,{analytics.TotalHours:F1}");
        csv.AppendLine($"Average Rating,{analytics.AverageRating:F1}");
        csv.AppendLine($"No-Show Rate,{analytics.NoShowRate:P1}");
        csv.AppendLine($"Verified Responses,{analytics.VerifiedResponses}");
        csv.AppendLine();

        // Recent Sessions
        if (analytics.RecentSessions.Any())
        {
            csv.AppendLine("RECENT SESSIONS");
            csv.AppendLine("Date,Module,Student,Duration (min),Rating,Status");

            foreach (var session in analytics.RecentSessions.OrderByDescending(x => x.ScheduledStart))
            {
                var status = session.IsNoShow ? "NoShow" : (session.Status == "Cancelled" ? "Cancelled" : "Completed");
                csv.AppendLine($"\"{session.ScheduledStart:yyyy-MM-dd HH:mm}\",\"{session.ModuleName}\",\"{session.StudentName}\",{session.DurationMinutes},{session.Rating?.ToString("F1") ?? ""},{status}");
            }
            csv.AppendLine();
        }

        // Top Students
        if (analytics.TopStudents.Any())
        {
            csv.AppendLine("TOP STUDENTS BY HOURS");
            csv.AppendLine("Student,Sessions,Total Hours,Average Rating");

            foreach (var student in analytics.TopStudents.OrderByDescending(x => x.TotalHours))
            {
                csv.AppendLine($"\"{student.StudentName}\",{student.SessionCount},{student.TotalHours:F1},{student.AverageRating?.ToString("F1") ?? ""}");
            }
            csv.AppendLine();
        }

        // Summary
        csv.AppendLine("SUMMARY");
        csv.AppendLine($"Over the past {days} days, this tutor has conducted {analytics.TotalSessions} sessions with {analytics.UniqueStudents} unique students, totaling {analytics.TotalHours:F1} hours of instruction. The average rating received is {analytics.AverageRating:F1} out of 5.0, with a no-show rate of {analytics.NoShowRate:P1}. Additionally, {analytics.VerifiedResponses} forum responses were verified by this tutor, demonstrating active engagement in the academic community.");

        return csv.ToString();
    }
}

public class PreviewSlotsRequest
{
    public int StudentId { get; set; }
    public int TutorId { get; set; }
    public int ModuleId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public StudentAvailabilityDto StudentAvailability { get; set; } = new();
}

public class ConfirmBookingRequest
{
    public Guid RequestId { get; set; }
    public List<DateTime> ApprovedSlots { get; set; } = new();
    public int TutorId { get; set; }
}

public class CancelSessionRequest
{
    public Guid SessionId { get; set; }
    public int UserId { get; set; }
    public string Reason { get; set; } = string.Empty;
}
