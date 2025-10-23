using Tutorly.Shared;

namespace Tutorly.Server.Services
{
    public interface IForumNotificationService
    {
        Task NotifyNewPostAsync(PostDto post, int communityId);
        Task NotifyNewResponseAsync(ResponseDto response, int postId);
        Task NotifyVoteUpdateAsync(int responseId, int newVoteCount);
        Task NotifyPostVoteUpdateAsync(int postId, int totalVoteCount);
        Task NotifyCommunityUpdateAsync(CommunityDto community);
        Task NotifyPostUpdateAsync(PostDto post);
    }
}
