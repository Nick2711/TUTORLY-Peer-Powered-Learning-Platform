using Tutorly.Shared;

namespace Tutorly.Server.Services
{
    public interface IAdminService
    {
        Task<ServiceResult<int>> GetTotalUsersCountAsync();
        Task<ServiceResult<AdminDashboardStats>> GetDashboardStatsAsync();
        Task<ServiceResult<List<UserDto>>> GetAllUsersAsync();
        Task<ServiceResult> UpdateUserStatusAsync(string userId, string status);
        Task<ServiceResult> WarnUserAsync(string userId, string adminId, string message);
        Task<ServiceResult<List<UserWarningDto>>> GetUserWarningsAsync(string userId);
        Task<ServiceResult<string>> AddUserAsync(AddUserRequest request);
        Task<ServiceResult> DeleteUserAsync(string userId);
    }

    public class AdminDashboardStats
    {
        public int TotalUsers { get; set; }
        public int TotalStudents { get; set; }
        public int TotalTutors { get; set; }
        public int TotalAdmins { get; set; }
        public int FlaggedContent { get; set; }
        public int PendingApprovals { get; set; }
        public int TotalModules { get; set; }
    }

    public class UserWarningDto
    {
        public int WarningId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string WarnedByAdminId { get; set; } = string.Empty;
        public string WarningMessage { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class AddUserRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty; // Student, Tutor, Admin
    }
}
