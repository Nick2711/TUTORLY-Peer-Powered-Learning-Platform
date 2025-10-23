using Microsoft.AspNetCore.Mvc;
using Tutorly.Server.Services;
using Tutorly.Shared;

namespace Tutorly.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AvailabilityController : ControllerBase
{
    private readonly IAvailabilityService _availabilityService;

    public AvailabilityController(IAvailabilityService availabilityService)
    {
        _availabilityService = availabilityService;
    }

    [HttpGet("tutor/{tutorId}")]
    public async Task<ActionResult<List<AvailabilityBlockDto>>> GetTutorAvailability(int tutorId, [FromQuery] int? moduleId = null)
    {
        try
        {
            var availability = await _availabilityService.GetTutorAvailabilityAsync(tutorId, moduleId);
            return Ok(availability);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving tutor availability: {ex.Message}");
        }
    }

    [HttpPost("tutor")]
    public async Task<ActionResult<ServiceResult>> SetTutorAvailability([FromBody] SetTutorAvailabilityRequest request)
    {
        try
        {
            var result = await _availabilityService.SetTutorAvailabilityAsync(request.TutorId, request.AvailabilityBlocks);

            if (result.Success)
                return Ok(result);

            return BadRequest(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error setting tutor availability: {ex.Message}");
        }
    }

    [HttpPost("tutor/exception")]
    public async Task<ActionResult<ServiceResult>> AddAvailabilityException([FromBody] AvailabilityExceptionDto exception)
    {
        try
        {
            var result = await _availabilityService.AddAvailabilityExceptionAsync(exception.TutorId, exception);

            if (result.Success)
                return Ok(result);

            return BadRequest(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error adding availability exception: {ex.Message}");
        }
    }

    [HttpDelete("tutor/{availabilityId}")]
    public async Task<ActionResult<ServiceResult>> DeleteAvailability(Guid availabilityId)
    {
        try
        {
            var result = await _availabilityService.DeleteAvailabilityAsync(availabilityId);

            if (result.Success)
                return Ok(result);

            return BadRequest(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error deleting availability: {ex.Message}");
        }
    }

    [HttpPost("student")]
    public async Task<ActionResult<ServiceResult>> SaveStudentAvailability([FromBody] SaveStudentAvailabilityRequest request)
    {
        try
        {
            var result = await _availabilityService.SaveStudentAvailabilityAsync(
                request.StudentAvailability,
                request.StudentId,
                request.BookingRequestId);

            if (result.Success)
                return Ok(result);

            return BadRequest(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error saving student availability: {ex.Message}");
        }
    }

    [HttpGet("tutor/{tutorId}/exceptions")]
    public async Task<ActionResult<List<AvailabilityExceptionDto>>> GetTutorExceptions(int tutorId, [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var exceptions = await _availabilityService.GetTutorExceptionsAsync(tutorId, startDate, endDate);
            return Ok(exceptions);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving tutor exceptions: {ex.Message}");
        }
    }

    [HttpDelete("tutor/exception/{exceptionId}")]
    public async Task<ActionResult<ServiceResult>> DeleteAvailabilityException(Guid exceptionId)
    {
        try
        {
            var result = await _availabilityService.DeleteAvailabilityExceptionAsync(exceptionId);

            if (result.Success)
                return Ok(result);

            return BadRequest(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error deleting availability exception: {ex.Message}");
        }
    }
}

public class SetTutorAvailabilityRequest
{
    public int TutorId { get; set; }
    public List<AvailabilityBlockDto> AvailabilityBlocks { get; set; } = new();
}

public class SaveStudentAvailabilityRequest
{
    public int StudentId { get; set; }
    public StudentAvailabilityDto StudentAvailability { get; set; } = new();
    public Guid? BookingRequestId { get; set; }
}
