using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tutorly.Server.Services;
using Tutorly.Shared;
using System.Security.Claims;

namespace Tutorly.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Require authentication for all admin endpoints
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;
        private readonly IAdminModuleService _adminModuleService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            IAdminService adminService,
            IAdminModuleService adminModuleService,
            ILogger<AdminController> logger)
        {
            _adminService = adminService;
            _adminModuleService = adminModuleService;
            _logger = logger;
        }

        /// <summary>
        /// Get the total number of users in the system
        /// </summary>
        /// <returns>Total users count</returns>
        [HttpGet("total-users")]
        public async Task<IActionResult> GetTotalUsers()
        {
            try
            {
                var result = await _adminService.GetTotalUsersCountAsync();

                if (result.Success)
                {
                    return Ok(new { totalUsers = result.Data });
                }

                return BadRequest(new { error = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetTotalUsers endpoint");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Get comprehensive dashboard statistics for admin
        /// </summary>
        /// <returns>Dashboard statistics</returns>
        [HttpGet("dashboard-stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            try
            {
                var result = await _adminService.GetDashboardStatsAsync();

                if (result.Success)
                {
                    return Ok(result.Data);
                }

                return BadRequest(new { error = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetDashboardStats endpoint");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Get all users in the system
        /// </summary>
        /// <returns>List of all users with their details</returns>
        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                var result = await _adminService.GetAllUsersAsync();

                if (result.Success)
                {
                    return Ok(result.Data);
                }

                return BadRequest(new { error = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAllUsers endpoint");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Update user status (Active, Suspended, Banned)
        /// </summary>
        /// <param name="userId">User ID to update</param>
        /// <param name="request">Status update request</param>
        /// <returns>Success or error message</returns>
        [HttpPut("users/{userId}/status")]
        public async Task<IActionResult> UpdateUserStatus(string userId, [FromBody] UpdateUserStatusRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Status))
                {
                    return BadRequest(new { error = "Status is required" });
                }

                var validStatuses = new[] { "Active", "Suspended", "Banned" };
                if (!validStatuses.Contains(request.Status))
                {
                    return BadRequest(new { error = "Invalid status. Must be Active, Suspended, or Banned" });
                }

                var result = await _adminService.UpdateUserStatusAsync(userId, request.Status);

                if (result.Success)
                {
                    return Ok(new { message = result.Message });
                }

                return BadRequest(new { error = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateUserStatus endpoint for userId {UserId}", userId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Send a warning to a user
        /// </summary>
        /// <param name="userId">User ID to warn</param>
        /// <param name="request">Warning request</param>
        /// <returns>Success or error message</returns>
        [HttpPost("users/{userId}/warn")]
        public async Task<IActionResult> WarnUser(string userId, [FromBody] WarnUserRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Message))
                {
                    return BadRequest(new { error = "Warning message is required" });
                }

                // Get admin ID from JWT claims
                var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(adminId))
                {
                    return Unauthorized(new { error = "Admin ID not found in token" });
                }

                var result = await _adminService.WarnUserAsync(userId, adminId, request.Message);

                if (result.Success)
                {
                    return Ok(new { message = result.Message });
                }

                return BadRequest(new { error = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in WarnUser endpoint for userId {UserId}", userId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Get warning history for a user
        /// </summary>
        /// <param name="userId">User ID to get warnings for</param>
        /// <returns>List of warnings</returns>
        [HttpGet("users/{userId}/warnings")]
        public async Task<IActionResult> GetUserWarnings(string userId)
        {
            try
            {
                var result = await _adminService.GetUserWarningsAsync(userId);

                if (result.Success)
                {
                    return Ok(result.Data);
                }

                return BadRequest(new { error = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetUserWarnings endpoint for userId {UserId}", userId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPost("users")]
        public async Task<IActionResult> AddUser([FromBody] AddUserRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new { error = "Request body is required" });
                }

                var result = await _adminService.AddUserAsync(request);

                if (result.Success)
                {
                    return Ok(new { message = "User created successfully", userId = result.Data });
                }
                return BadRequest(new { error = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AddUser endpoint");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpDelete("users/{userId}")]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            var result = await _adminService.DeleteUserAsync(userId);

            if (!result.Success)
            {
                return BadRequest(new { error = result.Message });
            }

            return Ok(new { message = result.Message });
        }

        // ========== MODULE MANAGEMENT ENDPOINTS ==========

        /// <summary>
        /// Get all modules with their counts
        /// </summary>
        /// <returns>List of all modules</returns>
        [HttpGet("modules")]
        public async Task<IActionResult> GetAllModules()
        {
            try
            {
                var result = await _adminModuleService.GetAllModulesAsync();

                if (result.Success)
                {
                    return Ok(result.Data);
                }

                return BadRequest(new { error = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAllModules endpoint");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Get a specific module by ID
        /// </summary>
        /// <param name="id">Module ID</param>
        /// <returns>Module details</returns>
        [HttpGet("modules/{id}")]
        public async Task<IActionResult> GetModuleById(int id)
        {
            try
            {
                var result = await _adminModuleService.GetModuleByIdAsync(id);

                if (result.Success)
                {
                    return Ok(result.Data);
                }

                return BadRequest(new { error = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetModuleById endpoint for module {ModuleId}", id);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Create a new module
        /// </summary>
        /// <param name="request">Module creation request</param>
        /// <returns>Created module</returns>
        [HttpPost("modules")]
        public async Task<IActionResult> CreateModule([FromBody] CreateModuleRequest request)
        {
            try
            {
                _logger.LogInformation("🔍 CreateModule endpoint called with request: {@Request}", request);

                if (request == null)
                {
                    _logger.LogWarning("❌ Request body is null");
                    return BadRequest(new { error = "Request body is required" });
                }

                _logger.LogInformation("✅ Request validation passed, calling service...");
                var result = await _adminModuleService.CreateModuleAsync(request);

                _logger.LogInformation("🔍 Service result: Success={Success}, Message={Message}", result.Success, result.Message);

                if (result.Success)
                {
                    _logger.LogInformation("✅ Module created successfully: {@Module}", result.Data);
                    return Ok(new { message = "Module created successfully", module = result.Data });
                }

                _logger.LogWarning("❌ Module creation failed: {Message}", result.Message);
                return BadRequest(new { error = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in CreateModule endpoint: {Message}", ex.Message);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Update an existing module
        /// </summary>
        /// <param name="id">Module ID</param>
        /// <param name="request">Module update request</param>
        /// <returns>Updated module</returns>
        [HttpPut("modules/{id}")]
        public async Task<IActionResult> UpdateModule(int id, [FromBody] UpdateModuleRequest request)
        {
            try
            {
                _logger.LogInformation("🔍 UpdateModule endpoint called with ID: {ModuleId}, Request: {@Request}", id, request);

                if (request == null)
                {
                    _logger.LogWarning("❌ Request body is null");
                    return BadRequest(new { error = "Request body is required" });
                }

                _logger.LogInformation("✅ Request validation passed, calling service...");
                var result = await _adminModuleService.UpdateModuleAsync(id, request);

                _logger.LogInformation("🔍 Service result: Success={Success}, Message={Message}", result.Success, result.Message);

                if (result.Success)
                {
                    _logger.LogInformation("✅ Module updated successfully: {@Module}", result.Data);
                    return Ok(new { message = "Module updated successfully", module = result.Data });
                }

                _logger.LogWarning("❌ Module update failed: {Message}", result.Message);
                return BadRequest(new { error = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in UpdateModule endpoint for module {ModuleId}: {Message}", id, ex.Message);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Delete a module and all related data
        /// </summary>
        /// <param name="id">Module ID</param>
        /// <returns>Success message</returns>
        [HttpDelete("modules/{id}")]
        public async Task<IActionResult> DeleteModule(int id)
        {
            try
            {
                _logger.LogInformation("🔍 DeleteModule endpoint called with ID: {ModuleId}", id);

                var result = await _adminModuleService.DeleteModuleAsync(id);

                _logger.LogInformation("🔍 Service result: Success={Success}, Message={Message}", result.Success, result.Message);

                if (result.Success)
                {
                    _logger.LogInformation("✅ Module {ModuleId} deleted successfully", id);
                    return Ok(new { message = result.Message });
                }

                _logger.LogWarning("❌ Module deletion failed: {Message}", result.Message);
                return BadRequest(new { error = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in DeleteModule endpoint for module {ModuleId}: {Message}", id, ex.Message);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }

    public class UpdateUserStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }

    public class WarnUserRequest
    {
        public string Message { get; set; } = string.Empty;
    }
}
