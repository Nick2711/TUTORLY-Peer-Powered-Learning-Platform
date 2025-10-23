using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Tutorly.Server.Services;
using Tutorly.Shared;

namespace Tutorly.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ModuleTutorController : ControllerBase
    {
        private readonly IModuleTutorService _moduleTutorService;
        private readonly ILogger<ModuleTutorController> _logger;

        public ModuleTutorController(IModuleTutorService moduleTutorService, ILogger<ModuleTutorController> logger)
        {
            _moduleTutorService = moduleTutorService;
            _logger = logger;
        }

        // POST: api/moduletutor/assign
        [HttpPost("assign")]
        public async Task<ActionResult<ApiResponse<bool>>> AssignTutorToModule([FromBody] AssignTutorToModuleDto dto)
        {
            try
            {
                var result = await _moduleTutorService.AssignTutorToModuleAsync(dto.TutorId, dto.ModuleId);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning tutor to module");
                return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Internal server error" });
            }
        }

        // POST: api/moduletutor/unassign
        [HttpPost("unassign")]
        public async Task<ActionResult<ApiResponse<bool>>> UnassignTutorFromModule([FromBody] AssignTutorToModuleDto dto)
        {
            try
            {
                var result = await _moduleTutorService.UnassignTutorFromModuleAsync(dto.TutorId, dto.ModuleId);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unassigning tutor from module");
                return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Internal server error" });
            }
        }
    }

    public class AssignTutorToModuleDto
    {
        public int TutorId { get; set; }
        public int ModuleId { get; set; }
    }
}
