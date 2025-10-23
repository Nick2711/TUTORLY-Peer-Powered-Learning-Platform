namespace Tutorly.Shared
{
    // Community DTOs
    public class CommunityDto
    {
        public int CommunityId { get; set; }
        public string CommunityName { get; set; } = string.Empty;
        public string? CommunityDescription { get; set; }
        public string? CommunityType { get; set; }
        public int? CreatedByStudentId { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
        public int Members { get; set; }
        public int Posts { get; set; }
        public bool Joined { get; set; }
        public int? ModuleId { get; set; }
    }

    public class CreateCommunityDto
    {
        public string CommunityName { get; set; } = string.Empty;
        public string? CommunityDescription { get; set; }
        public string? CommunityType { get; set; } = "course";
        public int? ModuleId { get; set; }
    }

    // Thread DTOs
    public class ThreadDto
    {
        public int ThreadId { get; set; }
        public int CommunityId { get; set; }
        public string ThreadName { get; set; } = string.Empty;
        public string? ThreadDescription { get; set; }
        public int? CreatedByStudentId { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
        public int Posts { get; set; }
    }

    public class CreateThreadDto
    {
        public string ThreadName { get; set; } = string.Empty;
        public string? ThreadDescription { get; set; }
    }

    // Post DTOs
    public class PostDto
    {
        public int ForumPostsId { get; set; }
        public int? CreatedByStudentId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public bool IsAnonymous { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? ModuleId { get; set; }
        public string PostType { get; set; } = "question";
        public string? Tag { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public int Votes { get; set; }
        public int Comments { get; set; }
        public int Saves { get; set; }
        public string TimeAgo { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
    }

    public class CreatePostDto
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public bool IsAnonymous { get; set; } = false;
        public string PostType { get; set; } = "question";
        public string? Tag { get; set; }
        public int? ModuleId { get; set; }
    }

    // Response DTOs
    public class ResponseDto
    {
        public int ForumResponsesId { get; set; }
        public int ForumPostsId { get; set; }
        public int? CreatedByStudentId { get; set; }
        public int? CreatedByTutorId { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsTutorVerified { get; set; }
        public int? VerifiedByTutorId { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public int Votes { get; set; }
        public string TimeAgo { get; set; } = string.Empty;
    }

    public class CreateResponseDto
    {
        public string Content { get; set; } = string.Empty;
        public int? MaterialsId { get; set; }
    }

    // Vote DTOs
    public class VoteDto
    {
        public int ForumVotesId { get; set; }
        public int ForumResponsesId { get; set; }
        public int StudentId { get; set; }
        public int VoteType { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateVoteDto
    {
        public int VoteType { get; set; } // 1 for upvote, -1 for downvote
    }

    // API Response DTOs
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class PaginatedResponse<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    // Filter and Sort DTOs
    public class PostFilterDto
    {
        public string? SortBy { get; set; } = "hot"; // hot, new, top
        public string? PostType { get; set; }
        public string? Tag { get; set; }
        public int? ModuleId { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class CommunityFilterDto
    {
        public string? SearchQuery { get; set; }
        public string? SortBy { get; set; } = "activity"; // activity, az, members
        public string? CommunityType { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    // Forum Metrics DTOs
    public class ForumMetricsDto
    {
        public int ActiveStudents { get; set; }
        public int PostsThisWeek { get; set; }
        public int TotalCommunities { get; set; }
        public int SolvedQuestions { get; set; }
    }

    // Forum Report DTOs
    public class CreateReportDto
    {
        public string ReportType { get; set; } = "post"; // post, response, resource
        public int ReportedItemId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? Details { get; set; }
        public string Severity { get; set; } = "mild"; // mild, moderate, severe
    }

    public class ForumReportDto
    {
        public int ReportId { get; set; }
        public string ReporterName { get; set; } = string.Empty;
        public string ReportType { get; set; } = string.Empty;
        public int ReportedItemId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? Details { get; set; }
        public string Severity { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string? ResolvedByUserId { get; set; }
        public string? ResolutionNotes { get; set; }
        
        // Additional context for display
        public string? ReportedContent { get; set; }
        public string? ReportedBy { get; set; }
        public string? ItemTitle { get; set; }
    }

    public class UpdateReportStatusDto
    {
        public string Status { get; set; } = string.Empty; // open, resolved, dismissed
        public string? ResolutionNotes { get; set; }
    }

    // User Warning DTOs
    public class CreateWarningDto
    {
        public string UserId { get; set; } = string.Empty;
        public string WarningMessage { get; set; } = string.Empty;
    }

    public class WarningDto
    {
        public int WarningId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string WarnedByAdminId { get; set; } = string.Empty;
        public string WarningMessage { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        
        // Additional context for display
        public string? AdminName { get; set; }
        public string? UserName { get; set; }
    }

    // User Ban DTOs
    public class CreateBanDto
    {
        public string UserId { get; set; } = string.Empty;
        public string BanReason { get; set; } = string.Empty;
        public string BanType { get; set; } = "temporary"; // temporary, permanent, auto
        public DateTime? ExpiresAt { get; set; }
    }

    public class UnbanUserDto
    {
        public string UnbanReason { get; set; } = string.Empty;
    }

    public class BanDto
    {
        public int BanId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string BannedByAdminId { get; set; } = string.Empty;
        public string BanReason { get; set; } = string.Empty;
        public string BanType { get; set; } = string.Empty;
        public DateTime BannedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsActive { get; set; }
        public DateTime? UnbannedAt { get; set; }
        public string? UnbannedByAdminId { get; set; }
        public string? UnbanReason { get; set; }
        
        // Additional context for display
        public string? AdminName { get; set; }
        public string? UserName { get; set; }
        public bool IsExpired { get; set; }
    }
}

