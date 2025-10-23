using Tutorly.Shared;

namespace Tutorly.Server.Services
{
    public interface IForumService
    {
        // Community operations
        Task<ApiResponse<CommunityDto>> CreateCommunityAsync(CreateCommunityDto dto, string userId);
        Task<ApiResponse<List<CommunityDto>>> GetCommunitiesAsync(CommunityFilterDto filter, string? userId = null);
        Task<ApiResponse<CommunityDto>> GetCommunityByIdAsync(int communityId);
        Task<ApiResponse<bool>> JoinCommunityAsync(int communityId, string userId);
        Task<ApiResponse<bool>> LeaveCommunityAsync(int communityId, string userId);

        // Thread operations
        Task<ApiResponse<ThreadDto>> CreateThreadAsync(int communityId, CreateThreadDto dto, string userId);
        Task<ApiResponse<List<ThreadDto>>> GetThreadsByCommunityAsync(int communityId);
        Task<ApiResponse<ThreadDto>> GetThreadByIdAsync(int threadId);

        // Post operations
        Task<ApiResponse<PostDto>> CreatePostAsync(int threadId, CreatePostDto dto, string userId);
        Task<ApiResponse<List<PostDto>>> GetPostsByThreadAsync(int threadId, PostFilterDto filter);
        Task<ApiResponse<PostDto>> GetPostByIdAsync(int postId);
        Task<ApiResponse<PostDto>> UpdatePostAsync(int postId, CreatePostDto dto, string userId);
        Task<ApiResponse<bool>> DeletePostAsync(int postId, string userId);

        // Response operations
        Task<ApiResponse<ResponseDto>> CreateResponseAsync(int postId, CreateResponseDto dto, string userId);
        Task<ApiResponse<List<ResponseDto>>> GetResponsesByPostAsync(int postId);
        Task<ApiResponse<ResponseDto>> UpdateResponseAsync(int responseId, CreateResponseDto dto, string userId);
        Task<ApiResponse<bool>> DeleteResponseAsync(int responseId, string userId);

        // Vote operations
        Task<ApiResponse<VoteDto>> VoteOnResponseAsync(int responseId, CreateVoteDto dto, string userId);
        Task<ApiResponse<VoteDto>> VoteOnPostAsync(int postId, CreateVoteDto dto, string userId);
        Task<ApiResponse<bool>> RemoveVoteAsync(int responseId, string userId);
        Task<ApiResponse<int>> GetResponseVoteCountAsync(int responseId);

        // Utility operations
        Task<ApiResponse<List<PostDto>>> GetRecentPostsAsync(PostFilterDto filter);
        Task<ApiResponse<List<PostDto>>> GetTrendingPostsAsync();
        Task<ApiResponse<List<CommunityDto>>> GetPopularCommunitiesAsync();
        Task<ApiResponse<ForumMetricsDto>> GetForumMetricsAsync();
        Task<ApiResponse<bool>> SavePostAsync(int postId, string userId);
        Task<ApiResponse<bool>> UnsavePostAsync(int postId, string userId);
        Task<ApiResponse<List<PostDto>>> GetSavedPostsAsync(string userId);

        // Report operations
        Task<ApiResponse<bool>> CreateReportAsync(CreateReportDto dto, string userId, string reportType, int reportedItemId);
        Task<ApiResponse<List<ForumReportDto>>> GetAllReportsAsync();
        Task<ApiResponse<bool>> UpdateReportStatusAsync(int reportId, UpdateReportStatusDto dto, string userId);

        // Warning operations
        Task<ApiResponse<bool>> CreateWarningAsync(CreateWarningDto dto, string adminUserId);
        Task<ApiResponse<List<WarningDto>>> GetWarningsByUserIdAsync(string userId);
        Task<ApiResponse<List<WarningDto>>> GetAllWarningsAsync();

        // Ban operations
        Task<ApiResponse<bool>> CreateBanAsync(CreateBanDto dto, string adminUserId);
        Task<ApiResponse<bool>> UnbanUserAsync(int banId, UnbanUserDto dto, string adminUserId);
        Task<ApiResponse<List<BanDto>>> GetBansByUserIdAsync(string userId);
        Task<ApiResponse<List<BanDto>>> GetAllBansAsync();
        Task<ApiResponse<bool>> IsUserBannedAsync(string userId);
        Task<ApiResponse<bool>> CheckAndAutoBanUserAsync(string userId);

        // Module community operations
        Task<ApiResponse<bool>> AutoFollowModuleCommunityAsync(int tutorId, int moduleId);
        Task<ApiResponse<bool>> AutoUnfollowModuleCommunityAsync(int tutorId, int moduleId);
    }
}

