using System.ComponentModel.DataAnnotations;

namespace Tutorly.Shared
{
    public class TutorApplicationDto
    {
        public int ApplicationId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string Programme { get; set; } = string.Empty;
        public int YearOfStudy { get; set; }
        public decimal? GPA { get; set; }
        public string? TranscriptUrl { get; set; }
        public string? TranscriptFilename { get; set; }
        public string Motivation { get; set; } = string.Empty;
        public string? PreviousExperience { get; set; }
        public string SubjectsInterested { get; set; } = string.Empty;
        public string? Availability { get; set; }
        public string Status { get; set; } = "pending";
        public string? AdminNotes { get; set; }
        public string? ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreateTutorApplicationDto
    {
        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        public string? Phone { get; set; }

        [Required]
        public string Programme { get; set; } = string.Empty;

        [Required]
        [Range(1, 5)]
        public int YearOfStudy { get; set; }

        public decimal? GPA { get; set; }

        [Required]
        public string Motivation { get; set; } = string.Empty;

        public string? PreviousExperience { get; set; }

        [Required]
        public string SubjectsInterested { get; set; } = string.Empty;

        public string? Availability { get; set; }
    }

    public class UpdateTutorApplicationDto
    {
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Programme { get; set; }
        public int? YearOfStudy { get; set; }
        public decimal? GPA { get; set; }
        public string? Motivation { get; set; }
        public string? PreviousExperience { get; set; }
        public string? SubjectsInterested { get; set; }
        public string? Availability { get; set; }
    }

    public class TutorApplicationReviewDto
    {
        [Required]
        public string Status { get; set; } = string.Empty; // approved, rejected

        public string? AdminNotes { get; set; }
    }

    public class TutorApplicationFilterDto
    {
        public string? Status { get; set; }
        public string? Programme { get; set; }
        public int? YearOfStudy { get; set; }
        public string? SearchQuery { get; set; }
        public string? SortBy { get; set; } = "created_at";
        public string? SortOrder { get; set; } = "desc";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
