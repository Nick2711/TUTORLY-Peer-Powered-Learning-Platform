using Supabase;
using Tutorly.Server.DatabaseModels;
using Tutorly.Shared;

namespace Tutorly.Server.Services
{
    public class RatingService : IRatingService
    {
        private readonly Supabase.Client _supabase;

        public RatingService(Supabase.Client supabase)
        {
            _supabase = supabase;
        }

        public async Task<ServiceResult<TutorRatingDto>> CreateRatingAsync(CreateTutorRatingDto ratingDto)
        {
            try
            {
                Console.WriteLine($"DEBUG: Creating rating - TutorId: {ratingDto.TutorId}, StudentId: {ratingDto.StudentId}, ModuleId: {ratingDto.ModuleId}, Rating: {ratingDto.Rating}");

                // First, validate that the tutor exists in the database
                var tutorExists = await ValidateTutorExists(ratingDto.TutorId);
                if (!tutorExists)
                {
                    Console.WriteLine($"DEBUG: Tutor {ratingDto.TutorId} does not exist in database");
                    return ServiceResult<TutorRatingDto>.FailureResult($"Tutor with ID {ratingDto.TutorId} does not exist in the database");
                }

                // Check if student already rated this tutor for this module
                Console.WriteLine($"DEBUG: Checking for existing rating before creating new one");
                var existingRating = await GetExistingRatingAsync(ratingDto.TutorId, ratingDto.StudentId, ratingDto.ModuleId);

                if (!existingRating.Success)
                {
                    Console.WriteLine($"DEBUG: Error checking existing rating: {existingRating.Message}");
                    return ServiceResult<TutorRatingDto>.FailureResult($"Error checking existing rating: {existingRating.Message}");
                }

                if (existingRating.Data != null)
                {
                    Console.WriteLine($"DEBUG: Rating already exists for TutorId: {ratingDto.TutorId}, StudentId: {ratingDto.StudentId}, ModuleId: {ratingDto.ModuleId}");
                    return ServiceResult<TutorRatingDto>.FailureResult("You have already rated this tutor for this module");
                }

                Console.WriteLine($"DEBUG: No existing rating found, proceeding to create new rating");

                var rating = new TutorRatingEntity
                {
                    RatingId = Guid.NewGuid(),
                    TutorId = ratingDto.TutorId,
                    StudentId = ratingDto.StudentId,
                    ModuleId = ratingDto.ModuleId,
                    Rating = ratingDto.Rating,
                    Notes = ratingDto.Notes,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                Console.WriteLine($"DEBUG: Inserting rating entity with ID: {rating.RatingId}");
                await _supabase.From<TutorRatingEntity>().Insert(rating);
                Console.WriteLine($"DEBUG: Rating inserted successfully");

                var result = await MapRatingToDtoAsync(rating);
                Console.WriteLine($"DEBUG: Mapped rating to DTO successfully");
                return ServiceResult<TutorRatingDto>.SuccessResult(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: Error creating rating: {ex.Message}");
                Console.WriteLine($"DEBUG: Stack trace: {ex.StackTrace}");
                return ServiceResult<TutorRatingDto>.FailureResult($"Error creating rating: {ex.Message}");
            }
        }

        private async Task<bool> ValidateTutorExists(int tutorId)
        {
            try
            {
                Console.WriteLine($"DEBUG: Validating tutor {tutorId} exists");
                var tutor = await _supabase
                    .From<TutorProfileEntity>()
                    .Where(x => x.TutorId == tutorId)
                    .Single();

                bool exists = tutor != null;
                Console.WriteLine($"DEBUG: Tutor {tutorId} exists: {exists}");
                return exists;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: Error validating tutor {tutorId}: {ex.Message}");
                return false;
            }
        }

        public async Task<ServiceResult<TutorRatingDto>> UpdateRatingAsync(Guid ratingId, CreateTutorRatingDto ratingDto)
        {
            try
            {
                var rating = await _supabase
                    .From<TutorRatingEntity>()
                    .Where(x => x.RatingId == ratingId)
                    .Single();

                if (rating == null)
                {
                    return ServiceResult<TutorRatingDto>.FailureResult("Rating not found");
                }

                rating.Rating = ratingDto.Rating;
                rating.Notes = ratingDto.Notes;
                rating.UpdatedAt = DateTime.UtcNow;

                await _supabase.From<TutorRatingEntity>()
                    .Where(x => x.RatingId == ratingId)
                    .Set(x => x.Rating, rating.Rating)
                    .Set(x => x.Notes, rating.Notes ?? string.Empty)
                    .Set(x => x.UpdatedAt, rating.UpdatedAt)
                    .Update();

                var result = await MapRatingToDtoAsync(rating);
                return ServiceResult<TutorRatingDto>.SuccessResult(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating rating: {ex.Message}");
                return ServiceResult<TutorRatingDto>.FailureResult($"Error updating rating: {ex.Message}");
            }
        }

        public async Task<ServiceResult> DeleteRatingAsync(Guid ratingId)
        {
            try
            {
                await _supabase.From<TutorRatingEntity>()
                    .Where(x => x.RatingId == ratingId)
                    .Delete();

                return ServiceResult.SuccessResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting rating: {ex.Message}");
                return ServiceResult.FailureResult($"Error deleting rating: {ex.Message}");
            }
        }

        public async Task<ServiceResult<List<TutorRatingDto>>> GetTutorRatingsAsync(int tutorId)
        {
            try
            {
                var ratings = await _supabase
                    .From<TutorRatingEntity>()
                    .Where(x => x.TutorId == tutorId)
                    .Get();

                var result = new List<TutorRatingDto>();
                foreach (var rating in ratings.Models.OrderByDescending(x => x.CreatedAt))
                {
                    result.Add(await MapRatingToDtoAsync(rating));
                }

                return ServiceResult<List<TutorRatingDto>>.SuccessResult(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting tutor ratings: {ex.Message}");
                return ServiceResult<List<TutorRatingDto>>.FailureResult($"Error getting tutor ratings: {ex.Message}");
            }
        }

        public async Task<ServiceResult<List<TutorRatingDto>>> GetStudentRatingsAsync(int studentId)
        {
            try
            {
                var ratings = await _supabase
                    .From<TutorRatingEntity>()
                    .Where(x => x.StudentId == studentId)
                    .Get();

                var result = new List<TutorRatingDto>();
                foreach (var rating in ratings.Models.OrderByDescending(x => x.CreatedAt))
                {
                    result.Add(await MapRatingToDtoAsync(rating));
                }

                return ServiceResult<List<TutorRatingDto>>.SuccessResult(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting student ratings: {ex.Message}");
                return ServiceResult<List<TutorRatingDto>>.FailureResult($"Error getting student ratings: {ex.Message}");
            }
        }

        public async Task<ServiceResult<TutorRatingSummaryDto>> GetTutorRatingSummaryAsync(int tutorId)
        {
            try
            {
                Console.WriteLine($"DEBUG: Getting rating summary for tutor ID: {tutorId}");

                var ratings = await _supabase
                    .From<TutorRatingEntity>()
                    .Where(x => x.TutorId == tutorId)
                    .Get();

                Console.WriteLine($"DEBUG: Found {ratings.Models.Count} ratings for tutor {tutorId}");

                if (!ratings.Models.Any())
                {
                    Console.WriteLine($"DEBUG: No ratings found for tutor {tutorId}");
                    return ServiceResult<TutorRatingSummaryDto>.SuccessResult(new TutorRatingSummaryDto
                    {
                        TutorId = tutorId,
                        AverageRating = 0,
                        TotalRatings = 0
                    });
                }

                var ratingValues = ratings.Models.Select(r => r.Rating).ToList();
                var averageRating = ratingValues.Average();
                var totalRatings = ratingValues.Count;

                Console.WriteLine($"DEBUG: Calculated average rating: {averageRating} from {totalRatings} ratings");

                var summary = new TutorRatingSummaryDto
                {
                    TutorId = tutorId,
                    AverageRating = Math.Round(averageRating, 1),
                    TotalRatings = totalRatings,
                    FiveStarCount = ratingValues.Count(r => r == 5),
                    FourStarCount = ratingValues.Count(r => r == 4),
                    ThreeStarCount = ratingValues.Count(r => r == 3),
                    TwoStarCount = ratingValues.Count(r => r == 2),
                    OneStarCount = ratingValues.Count(r => r == 1)
                };

                Console.WriteLine($"DEBUG: Returning summary - Average: {summary.AverageRating}, Total: {summary.TotalRatings}");
                return ServiceResult<TutorRatingSummaryDto>.SuccessResult(summary);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: Error getting tutor rating summary: {ex.Message}");
                Console.WriteLine($"DEBUG: Stack trace: {ex.StackTrace}");
                return ServiceResult<TutorRatingSummaryDto>.FailureResult($"Error getting tutor rating summary: {ex.Message}");
            }
        }

        public async Task<ServiceResult<TutorRatingDto?>> GetExistingRatingAsync(int tutorId, int studentId, int moduleId)
        {
            try
            {
                Console.WriteLine($"DEBUG: Checking for existing rating - TutorId: {tutorId}, StudentId: {studentId}, ModuleId: {moduleId}");

                var ratings = await _supabase
                    .From<TutorRatingEntity>()
                    .Where(x => x.TutorId == tutorId)
                    .Where(x => x.StudentId == studentId)
                    .Where(x => x.ModuleId == moduleId)
                    .Get();

                var rating = ratings.Models.FirstOrDefault();

                if (rating == null)
                {
                    Console.WriteLine($"DEBUG: No existing rating found for TutorId: {tutorId}, StudentId: {studentId}, ModuleId: {moduleId}");
                    return ServiceResult<TutorRatingDto?>.SuccessResult(null);
                }

                Console.WriteLine($"DEBUG: Found existing rating - RatingId: {rating.RatingId}, Rating: {rating.Rating}");
                var result = await MapRatingToDtoAsync(rating);
                return ServiceResult<TutorRatingDto?>.SuccessResult(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: Error getting existing rating: {ex.Message}");
                return ServiceResult<TutorRatingDto?>.FailureResult($"Error getting existing rating: {ex.Message}");
            }
        }

        private async Task<TutorRatingDto> MapRatingToDtoAsync(TutorRatingEntity rating)
        {
            var dto = new TutorRatingDto
            {
                RatingId = rating.RatingId,
                TutorId = rating.TutorId,
                StudentId = rating.StudentId,
                ModuleId = rating.ModuleId,
                Rating = rating.Rating,
                Notes = rating.Notes,
                CreatedAt = rating.CreatedAt,
                UpdatedAt = rating.UpdatedAt
            };

            try
            {
                // Get student name
                var studentProfile = await _supabase
                    .From<StudentProfileEntity>()
                    .Where(x => x.StudentId == rating.StudentId)
                    .Single();
                dto.StudentName = studentProfile?.FullName ?? "Unknown Student";

                // Get module information
                var module = await _supabase
                    .From<ModuleEntity>()
                    .Where(x => x.ModuleId == rating.ModuleId)
                    .Single();
                dto.ModuleCode = module?.ModuleCode ?? "Unknown";
                dto.ModuleName = module?.ModuleName ?? "Unknown Module";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading related data for rating: {ex.Message}");
                dto.StudentName = "Unknown Student";
                dto.ModuleCode = "Unknown";
                dto.ModuleName = "Unknown Module";
            }

            return dto;
        }
    }
}
