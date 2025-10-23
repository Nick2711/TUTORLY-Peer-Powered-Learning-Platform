using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Tutorly.Server.Services;
using Tutorly.Shared;
using System.Security.Claims;

namespace Tutorly.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TutorApplicationController : ControllerBase
    {
        private readonly ITutorApplicationService _tutorApplicationService;
        private readonly ILogger<TutorApplicationController> _logger;

        public TutorApplicationController(
            ITutorApplicationService tutorApplicationService,
            ILogger<TutorApplicationController> logger)
        {
            _tutorApplicationService = tutorApplicationService;
            _logger = logger;
        }

        // POST: api/tutorapplication
        [HttpPost]
        public async Task<ActionResult<ApiResponse<TutorApplicationDto>>> CreateApplication([FromForm] CreateTutorApplicationDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new ApiResponse<TutorApplicationDto> 
                    { 
                        Success = false, 
                        Message = "User not authenticated" 
                    });
                }

                var transcriptFile = Request.Form.Files.FirstOrDefault();
                var result = await _tutorApplicationService.CreateApplicationAsync(dto, userId, transcriptFile);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating tutor application");
                return StatusCode(500, new ApiResponse<TutorApplicationDto> 
                { 
                    Success = false, 
                    Message = "Internal server error" 
                });
            }
        }

        // GET: api/tutorapplication/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<TutorApplicationDto>>> GetApplication(int id)
        {
            try
            {
                var result = await _tutorApplicationService.GetApplicationByIdAsync(id);
                
                if (!result.Success)
                {
                    return NotFound(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tutor application");
                return StatusCode(500, new ApiResponse<TutorApplicationDto> 
                { 
                    Success = false, 
                    Message = "Internal server error" 
                });
            }
        }

        // GET: api/tutorapplication/my-application
        [HttpGet("my-application")]
        public async Task<ActionResult<ApiResponse<TutorApplicationDto>>> GetMyApplication()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new ApiResponse<TutorApplicationDto> 
                    { 
                        Success = false, 
                        Message = "User not authenticated" 
                    });
                }

                var result = await _tutorApplicationService.GetApplicationByUserIdAsync(userId);
                
                if (!result.Success)
                {
                    return NotFound(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user's tutor application");
                return StatusCode(500, new ApiResponse<TutorApplicationDto> 
                { 
                    Success = false, 
                    Message = "Internal server error" 
                });
            }
        }

        // GET: api/tutorapplication/admin/applications
        [HttpGet("admin/applications")]
        public async Task<ActionResult<ApiResponse<List<TutorApplicationDto>>>> GetApplications([FromQuery] TutorApplicationFilterDto filter)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new ApiResponse<List<TutorApplicationDto>> 
                    { 
                        Success = false, 
                        Message = "User not authenticated" 
                    });
                }

                // TODO: Add admin role check here
                // if (!IsAdmin(userId))
                // {
                //     return Forbid();
                // }

                var result = await _tutorApplicationService.GetApplicationsAsync(filter);
                
                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tutor applications");
                return StatusCode(500, new ApiResponse<List<TutorApplicationDto>> 
                { 
                    Success = false, 
                    Message = "Internal server error" 
                });
            }
        }

        // PUT: api/tutorapplication/{id}
        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<TutorApplicationDto>>> UpdateApplication(int id, [FromBody] UpdateTutorApplicationDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new ApiResponse<TutorApplicationDto> 
                    { 
                        Success = false, 
                        Message = "User not authenticated" 
                    });
                }

                var result = await _tutorApplicationService.UpdateApplicationAsync(id, dto, userId);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating tutor application");
                return StatusCode(500, new ApiResponse<TutorApplicationDto> 
                { 
                    Success = false, 
                    Message = "Internal server error" 
                });
            }
        }

        // DELETE: api/tutorapplication/{id}
        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteApplication(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new ApiResponse<bool> 
                    { 
                        Success = false, 
                        Message = "User not authenticated" 
                    });
                }

                var result = await _tutorApplicationService.DeleteApplicationAsync(id, userId);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting tutor application");
                return StatusCode(500, new ApiResponse<bool> 
                { 
                    Success = false, 
                    Message = "Internal server error" 
                });
            }
        }

        // POST: api/tutorapplication/{id}/upload-transcript
        [HttpPost("{id}/upload-transcript")]
        public async Task<ActionResult<ApiResponse<string>>> UploadTranscript(int id, IFormFile transcriptFile)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new ApiResponse<string> 
                    { 
                        Success = false, 
                        Message = "User not authenticated" 
                    });
                }

                var result = await _tutorApplicationService.UploadTranscriptAsync(id, transcriptFile);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading transcript");
                return StatusCode(500, new ApiResponse<string> 
                { 
                    Success = false, 
                    Message = "Internal server error" 
                });
            }
        }

        // DELETE: api/tutorapplication/{id}/transcript
        [HttpDelete("{id}/transcript")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteTranscript(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new ApiResponse<bool> 
                    { 
                        Success = false, 
                        Message = "User not authenticated" 
                    });
                }

                var result = await _tutorApplicationService.DeleteTranscriptAsync(id);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting transcript");
                return StatusCode(500, new ApiResponse<bool> 
                { 
                    Success = false, 
                    Message = "Internal server error" 
                });
            }
        }

        // POST: api/tutorapplication/admin/{id}/review
        [HttpPost("admin/{id}/review")]
        public async Task<ActionResult<ApiResponse<TutorApplicationDto>>> ReviewApplication(int id, [FromBody] TutorApplicationReviewDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new ApiResponse<TutorApplicationDto> 
                    { 
                        Success = false, 
                        Message = "User not authenticated" 
                    });
                }

                // TODO: Add admin role check here
                // if (!IsAdmin(userId))
                // {
                //     return Forbid();
                // }

                var result = await _tutorApplicationService.ReviewApplicationAsync(id, dto, userId);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reviewing tutor application");
                return StatusCode(500, new ApiResponse<TutorApplicationDto> 
                { 
                    Success = false, 
                    Message = "Internal server error" 
                });
            }
        }

        private string? GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
    }
}
