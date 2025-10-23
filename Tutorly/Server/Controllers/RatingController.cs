using Microsoft.AspNetCore.Mvc;
using Tutorly.Server.Services;
using Tutorly.Shared;

namespace Tutorly.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RatingController : ControllerBase
    {
        private readonly IRatingService _ratingService;

        public RatingController(IRatingService ratingService)
        {
            _ratingService = ratingService;
        }

        [HttpPost("rate")]
        public async Task<ActionResult> RateTutor([FromBody] CreateTutorRatingDto ratingDto)
        {
            try
            {
                var result = await _ratingService.CreateRatingAsync(ratingDto);

                if (result.Success)
                {
                    return Ok(result.Data);
                }

                return BadRequest(result.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in RateTutor: {ex.Message}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("tutor/{tutorId}")]
        public async Task<ActionResult<List<TutorRatingDto>>> GetTutorRatings(int tutorId)
        {
            try
            {
                var result = await _ratingService.GetTutorRatingsAsync(tutorId);

                if (result.Success)
                {
                    return Ok(result.Data);
                }

                return BadRequest(result.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetTutorRatings: {ex.Message}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("tutor/{tutorId}/summary")]
        public async Task<ActionResult<TutorRatingSummaryDto>> GetTutorRatingSummary(int tutorId)
        {
            try
            {
                var result = await _ratingService.GetTutorRatingSummaryAsync(tutorId);

                if (result.Success)
                {
                    return Ok(result.Data);
                }

                return BadRequest(result.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetTutorRatingSummary: {ex.Message}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("student/{studentId}")]
        public async Task<ActionResult<List<TutorRatingDto>>> GetStudentRatings(int studentId)
        {
            try
            {
                var result = await _ratingService.GetStudentRatingsAsync(studentId);

                if (result.Success)
                {
                    return Ok(result.Data);
                }

                return BadRequest(result.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetStudentRatings: {ex.Message}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{ratingId}")]
        public async Task<ActionResult> UpdateRating(Guid ratingId, [FromBody] CreateTutorRatingDto ratingDto)
        {
            try
            {
                var result = await _ratingService.UpdateRatingAsync(ratingId, ratingDto);

                if (result.Success)
                {
                    return Ok(result.Data);
                }

                return BadRequest(result.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UpdateRating: {ex.Message}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpDelete("{ratingId}")]
        public async Task<ActionResult> DeleteRating(Guid ratingId)
        {
            try
            {
                var result = await _ratingService.DeleteRatingAsync(ratingId);

                if (result.Success)
                {
                    return Ok();
                }

                return BadRequest(result.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in DeleteRating: {ex.Message}");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
