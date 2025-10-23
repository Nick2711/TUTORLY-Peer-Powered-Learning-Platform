using Microsoft.AspNetCore.SignalR;
using Tutorly.Shared;

namespace Tutorly.Server.Hubs
{
    public class ForumHub : Hub
    {
        public async Task JoinCommunity(int communityId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Community_{communityId}");
        }

        public async Task LeaveCommunity(int communityId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Community_{communityId}");
        }

        public async Task JoinThread(int threadId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Thread_{threadId}");
        }

        public async Task LeaveThread(int threadId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Thread_{threadId}");
        }

        public async Task JoinPost(int postId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Post_{postId}");
        }

        public async Task LeavePost(int postId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Post_{postId}");
        }
    }
}
