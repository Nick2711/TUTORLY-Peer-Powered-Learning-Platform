namespace Tutorly.Shared
{
    public class UnifiedUserDto
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? EmailConfirmedAt { get; set; }
        public DateTime? LastSignInAt { get; set; }
    }

    public class UnifiedProfileDto
    {
        public string Role { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public DateTime CreatedAt { get; set; }

        // Student/Tutor specific fields
        public int? StudentId { get; set; }
        public int? TutorId { get; set; }
        public string? Programme { get; set; }
        public int? YearOfStudy { get; set; }

        // Admin specific fields
        public int? AdminId { get; set; }
        public bool? ActiveAdmin { get; set; }
    }

    public class UnifiedSessionDto
    {
        public UnifiedUserDto User { get; set; } = new();
        public string Role { get; set; } = string.Empty;
        public UnifiedProfileDto Profile { get; set; } = new();
        public bool IsAuthenticated { get; set; }
    }
}
