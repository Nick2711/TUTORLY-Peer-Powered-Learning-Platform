using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Tutorly.Server.Services;
using Tutorly.Server.Helpers;
using Tutorly.Shared;
using System.Text.Json;
using System.Security.Claims;

namespace Tutorly.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ForumController : ControllerBase
    {
        private readonly IForumService _forumService;
        private readonly ILogger<ForumController> _logger;

        public ForumController(IForumService forumService, ILogger<ForumController> logger)
        {
            _forumService = forumService;
            _logger = logger;
        }


        #region Community Endpoints

        // Create a new community (students only)
        [HttpPost("communities")]
        public async Task<ActionResult<ApiResponse<CommunityDto>>> CreateCommunity([FromBody] CreateCommunityDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new ApiResponse<CommunityDto> { Success = false, Message = "User not authenticated" });
                }

                var result = await _forumService.CreateCommunityAsync(dto, userId);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating community");
                return StatusCode(500, new ApiResponse<CommunityDto> { Success = false, Message = "Internal server error" });
            }
        }


        [HttpGet("communities")]
        public async Task<ActionResult<ApiResponse<List<CommunityDto>>>> GetCommunities([FromQuery] CommunityFilterDto filter)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _forumService.GetCommunitiesAsync(filter, userId);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting communities");
                return StatusCode(500, new ApiResponse<List<CommunityDto>> { Success = false, Message = "Internal server error" });
            }
        }


        [HttpGet("communities/{communityId}")]
        public async Task<ActionResult<ApiResponse<CommunityDto>>> GetCommunity(int communityId)
        {
            try
            {
                var result = await _forumService.GetCommunityByIdAsync(communityId);

                if (!result.Success)
                {
                    return NotFound(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting community by ID");
                return StatusCode(500, new ApiResponse<CommunityDto> { Success = false, Message = "Internal server error" });
            }
        }


        [HttpPost("communities/{communityId}/join")]
        public async Task<ActionResult<ApiResponse<bool>>> JoinCommunity(int communityId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new ApiResponse<bool> { Success = false, Message = "User not authenticated" });
                }

                var result = await _forumService.JoinCommunityAsync(communityId, userId);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining community");
                return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Internal server error" });
            }
        }

        [HttpPost("communities/{communityId}/leave")]
        public async Task<ActionResult<ApiResponse<bool>>> LeaveCommunity(int communityId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new ApiResponse<bool> { Success = false, Message = "User not authenticated" });
                }

                var result = await _forumService.LeaveCommunityAsync(communityId, userId);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leaving community");
                return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Internal server error" });
            }
        }

        #endregion

        #region Thread Endpoints

        // Create a new thread in a community (students only)
        [HttpPost("communities/{communityId}/threads")]
        public async Task<ActionResult<ApiResponse<ThreadDto>>> CreateThread(int communityId, [FromBody] CreateThreadDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new ApiResponse<ThreadDto> { Success = false, Message = "User not authenticated" });
                }

                var result = await _forumService.CreateThreadAsync(communityId, dto, userId);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating thread");
                return StatusCode(500, new ApiResponse<ThreadDto> { Success = false, Message = "Internal server error" });
            }
        }


        [HttpGet("communities/{communityId}/threads")]
        public async Task<ActionResult<ApiResponse<List<ThreadDto>>>> GetThreadsByCommunity(int communityId)
        {
            try
            {
                var result = await _forumService.GetThreadsByCommunityAsync(communityId);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting threads by community");
                return StatusCode(500, new ApiResponse<List<ThreadDto>> { Success = false, Message = "Internal server error" });
            }
        }

        // Get a specific thread by ID
        [HttpGet("threads/{threadId}")]
        public async Task<ActionResult<ApiResponse<ThreadDto>>> GetThread(int threadId)
        {
            try
            {
                var result = await _forumService.GetThreadByIdAsync(threadId);

                if (!result.Success)
                {
                    return NotFound(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting thread by ID");
                return StatusCode(500, new ApiResponse<ThreadDto> { Success = false, Message = "Internal server error" });
            }
        }

        #endregion

        #region Post Endpoints

        // Create a new post in a thread (students only)
        [HttpPost("threads/{threadId}/posts")]
        public async Task<ActionResult<ApiResponse<PostDto>>> CreatePost(int threadId, [FromBody] CreatePostDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new ApiResponse<PostDto> { Success = false, Message = "User not authenticated" });
                }

                var result = await _forumService.CreatePostAsync(threadId, dto, userId);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating post");
                return StatusCode(500, new ApiResponse<PostDto> { Success = false, Message = "Internal server error" });
            }
        }

        [HttpGet("threads/{threadId}/posts")]
        public async Task<ActionResult<ApiResponse<List<PostDto>>>> GetPostsByThread(int threadId, [FromQuery] PostFilterDto filter)
        {
            try
            {
                _logger.LogInformation($"GetPostsByThread: Getting posts for thread {threadId}");
                var result = await _forumService.GetPostsByThreadAsync(threadId, filter);

                if (!result.Success)
                {
                    _logger.LogWarning($"GetPostsByThread failed: {result.Message}");
                    return BadRequest(result);
                }

                _logger.LogInformation($"GetPostsByThread: Found {result.Data?.Count ?? 0} posts for thread {threadId}");
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting posts by thread {threadId}: {ex.Message}");
                return StatusCode(500, new ApiResponse<List<PostDto>> { Success = false, Message = $"Internal server error: {ex.Message}" });
            }
        }

        // Get a specific post by ID
        [HttpGet("posts/{postId}")]
        public async Task<ActionResult<ApiResponse<PostDto>>> GetPost(int postId)
        {
            try
            {
                var result = await _forumService.GetPostByIdAsync(postId);

                if (!result.Success)
                {
                    return NotFound(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting post by ID");
                return StatusCode(500, new ApiResponse<PostDto> { Success = false, Message = "Internal server error" });
            }
        }

        // Update a post  admin only)
        [HttpPut("posts/{postId}")]
        public async Task<ActionResult<ApiResponse<PostDto>>> UpdatePost(int postId, [FromBody] CreatePostDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new ApiResponse<PostDto> { Success = false, Message = "User not authenticated" });
                }

                var result = await _forumService.UpdatePostAsync(postId, dto, userId);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating post");
                return StatusCode(500, new ApiResponse<PostDto> { Success = false, Message = "Internal server error" });
            }
        }

        // Delete a post (admin only)
        [HttpDelete("posts/{postId}")]
        public async Task<ActionResult<ApiResponse<bool>>> DeletePost(int postId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new ApiResponse<bool> { Success = false, Message = "User not authenticated" });
                }

                var result = await _forumService.DeletePostAsync(postId, userId);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting post");
                return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Internal server error" });
            }
        }

        #endregion

        #region Response Endpoints

        // Create a new response to a post (students only)
        [HttpPost("posts/{postId}/responses")]
        public async Task<ActionResult<ApiResponse<ResponseDto>>> CreateResponse(int postId, [FromBody] CreateResponseDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new ApiResponse<ResponseDto> { Success = false, Message = "User not authenticated" });
                }

                var result = await _forumService.CreateResponseAsync(postId, dto, userId);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating response");
                return StatusCode(500, new ApiResponse<ResponseDto> { Success = false, Message = "Internal server error" });
            }
        }

        // Get all responses for a post
        [HttpGet("posts/{postId}/responses")]
        public async Task<ActionResult<ApiResponse<List<ResponseDto>>>> GetResponsesByPost(int postId)
        {
            try
            {
                var result = await _forumService.GetResponsesByPostAsync(postId);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting responses by post");
                return StatusCode(500, new ApiResponse<List<ResponseDto>> { Success = false, Message = "Internal server error" });
            }
        }

        // Update a response (owner or admin only)
        [HttpPut("responses/{responseId}")]
        public async Task<ActionResult<ApiResponse<ResponseDto>>> UpdateResponse(int responseId, [FromBody] CreateResponseDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new ApiResponse<ResponseDto> { Success = false, Message = "User not authenticated" });
                }

                var result = await _forumService.UpdateResponseAsync(responseId, dto, userId);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating response");
                return StatusCode(500, new ApiResponse<ResponseDto> { Success = false, Message = "Internal server error" });
            }
        }

        // Delete a response  admin only)
        [HttpDelete("responses/{responseId}")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteResponse(int responseId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new ApiResponse<bool> { Success = false, Message = "User not authenticated" });
                }

                var result = await _forumService.DeleteResponseAsync(responseId, userId);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting response");
                return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Internal server error" });
            }
        }

        #endregion

        #region Vote Endpoints

        // Vote on a response (students only)
        [HttpPost("responses/{responseId}/vote")]
        public async Task<ActionResult<ApiResponse<VoteDto>>> VoteOnResponse(int responseId, [FromBody] CreateVoteDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new ApiResponse<VoteDto> { Success = false, Message = "User not authenticated" });
                }

                var result = await _forumService.VoteOnResponseAsync(responseId, dto, userId);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error voting on response");
                return StatusCode(500, new ApiResponse<VoteDto> { Success = false, Message = "Internal server error" });
            }
        }

        // Remove vote from a response
        [HttpDelete("responses/{responseId}/vote")]
        public async Task<ActionResult<ApiResponse<bool>>> RemoveVote(int responseId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new ApiResponse<bool> { Success = false, Message = "User not authenticated" });
                }

                var result = await _forumService.RemoveVoteAsync(responseId, userId);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing vote");
                return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Internal server error" });
            }
        }

        // Get vote count for a response
        [HttpGet("responses/{responseId}/votes")]
        public async Task<ActionResult<ApiResponse<int>>> GetResponseVoteCount(int responseId)
        {
            try
            {
                var result = await _forumService.GetResponseVoteCountAsync(responseId);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting vote count");
                return StatusCode(500, new ApiResponse<int> { Success = false, Message = "Internal server error" });
            }
        }

        // Vote on a post (students only)
        [HttpPost("posts/{postId}/vote")]
        public async Task<ActionResult<ApiResponse<VoteDto>>> VoteOnPost(int postId, [FromBody] CreateVoteDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new ApiResponse<VoteDto> { Success = false, Message = "User not authenticated" });
                }

                var result = await _forumService.VoteOnPostAsync(postId, dto, userId);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error voting on post");
                return StatusCode(500, new ApiResponse<VoteDto> { Success = false, Message = "Internal server error" });
            }
        }

        [HttpDelete("posts/{postId}/vote")]
        public async Task<ActionResult<ApiResponse<bool>>> RemovePostVote(int postId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new ApiResponse<bool> { Success = false, Message = "User not authenticated" });
                }

                var result = await _forumService.RemoveVoteAsync(postId, userId);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing vote from post");
                return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Internal server error" });
            }
        }

        #endregion

        #region Utility Endpoints

        // Get recent posts with filtering
        [HttpGet("posts/recent")]
        public async Task<ActionResult<ApiResponse<List<PostDto>>>> GetRecentPosts([FromQuery] PostFilterDto filter)
        {
            try
            {
                var result = await _forumService.GetRecentPostsAsync(filter);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent posts");
                return StatusCode(500, new ApiResponse<List<PostDto>> { Success = false, Message = "Internal server error" });
            }
        }

        // Get trending posts
        [HttpGet("posts/trending")]
        public async Task<ActionResult<ApiResponse<List<PostDto>>>> GetTrendingPosts()
        {
            try
            {
                var result = await _forumService.GetTrendingPostsAsync();

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting trending posts");
                return StatusCode(500, new ApiResponse<List<PostDto>> { Success = false, Message = "Internal server error" });
            }
        }

        // Get popular communities
        [HttpGet("communities/popular")]
        public async Task<ActionResult<ApiResponse<List<CommunityDto>>>> GetPopularCommunities()
        {
            try
            {
                var result = await _forumService.GetPopularCommunitiesAsync();

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting popular communities");
                return StatusCode(500, new ApiResponse<List<CommunityDto>> { Success = false, Message = "Internal server error" });
            }
        }

        [HttpGet("metrics")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<ForumMetricsDto>>> GetForumMetrics()
        {
            try
            {
                var result = await _forumService.GetForumMetricsAsync();

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting forum metrics");
                return StatusCode(500, new ApiResponse<ForumMetricsDto> { Success = false, Message = "Internal server error" });
            }
        }


        [HttpPost("posts/{postId}/save")]
        public async Task<ActionResult<ApiResponse<bool>>> SavePost(int postId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new ApiResponse<bool> { Success = false, Message = "User not authenticated" });
                }

                var result = await _forumService.SavePostAsync(postId, userId);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving post");
                return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Internal server error" });
            }
        }


        [HttpDelete("posts/{postId}/save")]
        public async Task<ActionResult<ApiResponse<bool>>> UnsavePost(int postId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new ApiResponse<bool> { Success = false, Message = "User not authenticated" });
                }

                var result = await _forumService.UnsavePostAsync(postId, userId);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unsaving post");
                return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Internal server error" });
            }
        }


        [HttpGet("posts/saved")]
        public async Task<ActionResult<ApiResponse<List<PostDto>>>> GetSavedPosts()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new ApiResponse<List<PostDto>> { Success = false, Message = "User not authenticated" });
                }

                var result = await _forumService.GetSavedPostsAsync(userId);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting saved posts");
                return StatusCode(500, new ApiResponse<List<PostDto>> { Success = false, Message = "Internal server error" });
            }
        }

        #endregion

        #region Report Endpoints

        [HttpPost("posts/{postId}/report")]
        public async Task<ActionResult<ApiResponse<bool>>> ReportPost(int postId, [FromBody] CreateReportDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new ApiResponse<bool> { Success = false, Message = "User not authenticated" });
                }

                var result = await _forumService.CreateReportAsync(dto, userId, "post", postId);
                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reporting post {PostId}", postId);
                return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Internal server error" });
            }
        }

        // Create a new report for a forum response
        [HttpPost("responses/{responseId}/report")]
        public async Task<ActionResult<ApiResponse<bool>>> ReportResponse(int responseId, [FromBody] CreateReportDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new ApiResponse<bool> { Success = false, Message = "User not authenticated" });
                }

                var result = await _forumService.CreateReportAsync(dto, userId, "response", responseId);
                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reporting response {ResponseId}", responseId);
                return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Internal server error" });
            }
        }

        // Get all reports for admin moderation
        [HttpGet("reports")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<List<ForumReportDto>>>> GetReports()
        {
            try
            {
                var result = await _forumService.GetAllReportsAsync();
                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting reports");
                return StatusCode(500, new ApiResponse<List<ForumReportDto>> { Success = false, Message = "Internal server error" });
            }
        }

        // Update report status (admin only)
        [HttpPut("reports/{reportId}/status")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateReportStatus(int reportId, [FromBody] UpdateReportStatusDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new ApiResponse<bool> { Success = false, Message = "User not authenticated" });
                }

                var result = await _forumService.UpdateReportStatusAsync(reportId, dto, userId);
                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating report status {ReportId}", reportId);
                return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Internal server error" });
            }
        }

        #endregion

        #region Warning Endpoints

        // Create a warning for a user (admin only)
        [HttpPost("warnings")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<bool>>> CreateWarning([FromBody] CreateWarningDto dto)
        {
            try
            {
                var adminUserId = GetCurrentUserId();
                if (string.IsNullOrEmpty(adminUserId))
                {
                    // For testing purposes, use a default admin ID
                    adminUserId = "system";
                }

                var result = await _forumService.CreateWarningAsync(dto, adminUserId);
                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating warning");
                return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Internal server error" });
            }
        }


        // Get warnings for a specific user

        [HttpGet("warnings/user/{userId}")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<List<WarningDto>>>> GetWarningsByUser(string userId)
        {
            try
            {
                var result = await _forumService.GetWarningsByUserIdAsync(userId);
                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting warnings for user {UserId}", userId);
                return StatusCode(500, new ApiResponse<List<WarningDto>> { Success = false, Message = "Internal server error" });
            }
        }

        //Get all warnings (admin only)
  
        [HttpGet("warnings")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<List<WarningDto>>>> GetAllWarnings()
        {
            try
            {
                var result = await _forumService.GetAllWarningsAsync();
                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all warnings");
                return StatusCode(500, new ApiResponse<List<WarningDto>> { Success = false, Message = "Internal server error" });
            }
        }

        #endregion

        #region Ban Endpoints

        // Create a ban for a user (admin only)
        [HttpPost("bans")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<bool>>> CreateBan([FromBody] CreateBanDto dto)
        {
            try
            {
                var adminUserId = GetCurrentUserId();
                if (string.IsNullOrEmpty(adminUserId))
                {
                    // For testing purposes, use a default admin ID
                    adminUserId = "system";
                }

                var result = await _forumService.CreateBanAsync(dto, adminUserId);
                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating ban");
                return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Internal server error" });
            }
        }

        // Unban a user (admin only)
        [HttpPut("bans/{banId}/unban")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<bool>>> UnbanUser(int banId, [FromBody] UnbanUserDto dto)
        {
            try
            {
                var adminUserId = GetCurrentUserId();
                if (string.IsNullOrEmpty(adminUserId))
                {
                    return Unauthorized(new ApiResponse<bool> { Success = false, Message = "User not authenticated" });
                }

                var result = await _forumService.UnbanUserAsync(banId, dto, adminUserId);
                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unbanning user {BanId}", banId);
                return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Internal server error" });
            }
        }

        // Get bans for a specific user
        [HttpGet("bans/user/{userId}")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<List<BanDto>>>> GetBansByUser(string userId)
        {
            try
            {
                var result = await _forumService.GetBansByUserIdAsync(userId);
                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting bans for user {UserId}", userId);
                return StatusCode(500, new ApiResponse<List<BanDto>> { Success = false, Message = "Internal server error" });
            }
        }

        // Get all bans (admin only)
        [HttpGet("bans")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<List<BanDto>>>> GetAllBans()
        {
            try
            {
                var result = await _forumService.GetAllBansAsync();
                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all bans");
                return StatusCode(500, new ApiResponse<List<BanDto>> { Success = false, Message = "Internal server error" });
            }
        }

        // Check if user is banned
        [HttpGet("bans/check/{userId}")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<bool>>> IsUserBanned(string userId)
        {
            try
            {
                var result = await _forumService.IsUserBannedAsync(userId);
                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking ban status for user {UserId}", userId);
                return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Internal server error" });
            }
        }

        #endregion

        #region Helper Methods

        private string? GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }

        #endregion
    }
}

