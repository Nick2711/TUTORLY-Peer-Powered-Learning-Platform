using Microsoft.AspNetCore.SignalR;
using Tutorly.Server.Hubs;
using Tutorly.Shared;

namespace Tutorly.Server.Services
{
    public class ForumNotificationService : IForumNotificationService
    {
        private readonly IHubContext<ForumHub> _hubContext;
        private readonly ILogger<ForumNotificationService> _logger;

        public ForumNotificationService(IHubContext<ForumHub> hubContext, ILogger<ForumNotificationService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task NotifyNewPostAsync(PostDto post, int communityId)
        {
            try
            {
                await _hubContext.Clients.Group($"Community_{communityId}")
                    .SendAsync("NewPost", post);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying new post");
            }
        }

        public async Task NotifyNewResponseAsync(ResponseDto response, int postId)
        {
            try
            {
                await _hubContext.Clients.Group($"Post_{postId}")
                    .SendAsync("NewResponse", response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying new response");
            }
        }

        public async Task NotifyVoteUpdateAsync(int responseId, int newVoteCount)
        {
            try
            {
                await _hubContext.Clients.All
                    .SendAsync("VoteUpdate", new { ResponseId = responseId, VoteCount = newVoteCount });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying vote update");
            }
        }

        public async Task NotifyPostVoteUpdateAsync(int postId, int totalVoteCount)
        {
            try
            {
                _logger.LogInformation($"NotifyPostVoteUpdateAsync: Sending vote update for post {postId} with count {totalVoteCount}");
                await _hubContext.Clients.All
                    .SendAsync("PostVoteUpdate", new { PostId = postId, VoteCount = totalVoteCount });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying post vote update");
            }
        }

        public async Task NotifyCommunityUpdateAsync(CommunityDto community)
        {
            try
            {
                await _hubContext.Clients.All
                    .SendAsync("CommunityUpdate", community);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying community update");
            }
        }

        public async Task NotifyPostUpdateAsync(PostDto post)
        {
            try
            {
                await _hubContext.Clients.All
                    .SendAsync("PostUpdate", post);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying post update");
            }
        }
    }
}
