namespace Tutorly.Shared
{
    public class TutorRatingDto
    {
        public Guid RatingId { get; set; }
        public int TutorId { get; set; }
        public int StudentId { get; set; }
        public int ModuleId { get; set; }
        public int Rating { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Additional fields for display
        public string? StudentName { get; set; }
        public string? ModuleCode { get; set; }
        public string? ModuleName { get; set; }
    }

    public class CreateTutorRatingDto
    {
        public int TutorId { get; set; }
        public int StudentId { get; set; }
        public int ModuleId { get; set; }
        public int Rating { get; set; }
        public string? Notes { get; set; }
    }

    public class TutorRatingSummaryDto
    {
        public int TutorId { get; set; }
        public double AverageRating { get; set; }
        public int TotalRatings { get; set; }
        public int FiveStarCount { get; set; }
        public int FourStarCount { get; set; }
        public int ThreeStarCount { get; set; }
        public int TwoStarCount { get; set; }
        public int OneStarCount { get; set; }
    }
}
