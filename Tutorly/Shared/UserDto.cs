namespace Tutorly.Shared
{
    public class UserDto
    {
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty; // Student, Tutor, Admin
        public string Status { get; set; } = string.Empty; // Active, Suspended, Banned
        public DateTime JoinedDate { get; set; }
        public string? AvatarUrl { get; set; }
    }
}
