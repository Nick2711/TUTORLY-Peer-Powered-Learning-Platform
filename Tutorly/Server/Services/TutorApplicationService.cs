using Tutorly.Server.Helpers;
using Tutorly.Server.DatabaseModels;
using Tutorly.Shared;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using Supabase.Postgrest;
using Microsoft.AspNetCore.Http;

namespace Tutorly.Server.Services
{
    public class TutorApplicationService : ITutorApplicationService
    {
        private readonly ISupabaseClientFactory _supabaseFactory;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<TutorApplicationService> _logger;

        public TutorApplicationService(
            ISupabaseClientFactory supabaseFactory,
            IWebHostEnvironment environment,
            ILogger<TutorApplicationService> logger)
        {
            _supabaseFactory = supabaseFactory;
            _environment = environment;
            _logger = logger;
        }

        public async Task<ApiResponse<TutorApplicationDto>> CreateApplicationAsync(CreateTutorApplicationDto dto, string userId, IFormFile? transcriptFile)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                // Check if user already has an application
                var existingApplication = await client
                    .From<TutorApplicationEntity>()
                    .Where(x => x.UserId == userId)
                    .Get();

                if (existingApplication.Models.Any())
                {
                    return new ApiResponse<TutorApplicationDto> 
                    { 
                        Success = false, 
                        Message = "You already have a pending tutor application" 
                    };
                }

                var application = new TutorApplicationEntity
                {
                    UserId = userId,
                    FullName = dto.FullName,
                    Email = dto.Email,
                    Phone = dto.Phone,
                    Programme = dto.Programme,
                    YearOfStudy = dto.YearOfStudy,
                    GPA = dto.GPA,
                    Motivation = dto.Motivation,
                    PreviousExperience = dto.PreviousExperience,
                    SubjectsInterested = dto.SubjectsInterested,
                    Availability = dto.Availability,
                    Status = "pending",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var response = await client
                    .From<TutorApplicationEntity>()
                    .Insert(application);

                var createdApplication = response.Models.First();

                // Upload transcript if provided
                if (transcriptFile != null)
                {
                    var uploadResult = await UploadTranscriptAsync(createdApplication.ApplicationId, transcriptFile);
                    if (uploadResult.Success)
                    {
                        // Update application with transcript URL
                        await client
                            .From<TutorApplicationEntity>()
                            .Where(x => x.ApplicationId == createdApplication.ApplicationId)
                            .Set(x => x.TranscriptUrl, uploadResult.Data)
                            .Set(x => x.TranscriptFilename, transcriptFile.FileName)
                            .Update();
                    }
                }

                var applicationDto = MapToDto(createdApplication);
                return new ApiResponse<TutorApplicationDto> 
                { 
                    Success = true, 
                    Data = applicationDto, 
                    Message = "Tutor application submitted successfully" 
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating tutor application");
                return new ApiResponse<TutorApplicationDto> 
                { 
                    Success = false, 
                    Message = "Failed to create tutor application" 
                };
            }
        }

        public async Task<ApiResponse<TutorApplicationDto>> GetApplicationByIdAsync(int applicationId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();
                var response = await client
                    .From<TutorApplicationEntity>()
                    .Where(x => x.ApplicationId == applicationId)
                    .Get();

                if (!response.Models.Any())
                {
                    return new ApiResponse<TutorApplicationDto> 
                    { 
                        Success = false, 
                        Message = "Application not found" 
                    };
                }

                var applicationDto = MapToDto(response.Models.First());
                return new ApiResponse<TutorApplicationDto> 
                { 
                    Success = true, 
                    Data = applicationDto 
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tutor application by ID");
                return new ApiResponse<TutorApplicationDto> 
                { 
                    Success = false, 
                    Message = "Failed to get tutor application" 
                };
            }
        }

        public async Task<ApiResponse<TutorApplicationDto>> GetApplicationByUserIdAsync(string userId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();
                var response = await client
                    .From<TutorApplicationEntity>()
                    .Where(x => x.UserId == userId)
                    .Get();

                if (!response.Models.Any())
                {
                    return new ApiResponse<TutorApplicationDto> 
                    { 
                        Success = false, 
                        Message = "No application found for this user" 
                    };
                }

                var applicationDto = MapToDto(response.Models.First());
                return new ApiResponse<TutorApplicationDto> 
                { 
                    Success = true, 
                    Data = applicationDto 
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tutor application by user ID");
                return new ApiResponse<TutorApplicationDto> 
                { 
                    Success = false, 
                    Message = "Failed to get tutor application" 
                };
            }
        }

        public async Task<ApiResponse<List<TutorApplicationDto>>> GetApplicationsAsync(TutorApplicationFilterDto filter)
        {
            try
            {
                var client = _supabaseFactory.CreateService();
                
                // Build query with filters and sorting in one chain
                var ordering = filter.SortOrder == "asc" 
                    ? Supabase.Postgrest.Constants.Ordering.Ascending 
                    : Supabase.Postgrest.Constants.Ordering.Descending;

                Supabase.Postgrest.Interfaces.IPostgrestTable<TutorApplicationEntity> query = client.From<TutorApplicationEntity>();
                
                // Apply filters
                if (!string.IsNullOrEmpty(filter.Status))
                {
                    query = query.Where(x => x.Status == filter.Status);
                }

                if (!string.IsNullOrEmpty(filter.Programme))
                {
                    query = query.Where(x => x.Programme == filter.Programme);
                }

                if (filter.YearOfStudy.HasValue)
                {
                    query = query.Where(x => x.YearOfStudy == filter.YearOfStudy.Value);
                }

                if (!string.IsNullOrEmpty(filter.SearchQuery))
                {
                    query = query.Where(x => x.FullName.Contains(filter.SearchQuery) ||
                                           x.Email.Contains(filter.SearchQuery) ||
                                           x.Programme.Contains(filter.SearchQuery));
                }

                // Apply sorting based on sortBy
                var response = filter.SortBy switch
                {
                    "name" => await query.Order(x => x.FullName, ordering).Get(),
                    "email" => await query.Order(x => x.Email, ordering).Get(),
                    "programme" => await query.Order(x => x.Programme, ordering).Get(),
                    "status" => await query.Order(x => x.Status, ordering).Get(),
                    _ => await query.Order(x => x.CreatedAt, ordering).Get()
                };
                var applications = response.Models.Select(MapToDto).ToList();

                return new ApiResponse<List<TutorApplicationDto>> 
                { 
                    Success = true, 
                    Data = applications 
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tutor applications");
                return new ApiResponse<List<TutorApplicationDto>> 
                { 
                    Success = false, 
                    Message = "Failed to get tutor applications" 
                };
            }
        }

        public async Task<ApiResponse<TutorApplicationDto>> UpdateApplicationAsync(int applicationId, UpdateTutorApplicationDto dto, string userId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                // Check if application exists and belongs to user
                var existingApplication = await client
                    .From<TutorApplicationEntity>()
                    .Where(x => x.ApplicationId == applicationId && x.UserId == userId)
                    .Get();

                if (!existingApplication.Models.Any())
                {
                    return new ApiResponse<TutorApplicationDto> 
                    { 
                        Success = false, 
                        Message = "Application not found or you don't have permission to update it" 
                    };
                }

                var application = existingApplication.Models.First();

                // Only allow updates if status is pending
                if (application.Status != "pending")
                {
                    return new ApiResponse<TutorApplicationDto> 
                    { 
                        Success = false, 
                        Message = "Cannot update application that has already been reviewed" 
                    };
                }

                // Update fields
                if (!string.IsNullOrEmpty(dto.FullName))
                    application.FullName = dto.FullName;
                if (!string.IsNullOrEmpty(dto.Email))
                    application.Email = dto.Email;
                if (dto.Phone != null)
                    application.Phone = dto.Phone;
                if (!string.IsNullOrEmpty(dto.Programme))
                    application.Programme = dto.Programme;
                if (dto.YearOfStudy.HasValue)
                    application.YearOfStudy = dto.YearOfStudy.Value;
                if (dto.GPA.HasValue)
                    application.GPA = dto.GPA;
                if (!string.IsNullOrEmpty(dto.Motivation))
                    application.Motivation = dto.Motivation;
                if (dto.PreviousExperience != null)
                    application.PreviousExperience = dto.PreviousExperience;
                if (!string.IsNullOrEmpty(dto.SubjectsInterested))
                    application.SubjectsInterested = dto.SubjectsInterested;
                if (dto.Availability != null)
                    application.Availability = dto.Availability;

                application.UpdatedAt = DateTime.UtcNow;

                await client
                    .From<TutorApplicationEntity>()
                    .Where(x => x.ApplicationId == applicationId)
                    .Set(x => x.FullName, application.FullName)
                    .Set(x => x.Email, application.Email)
                    .Set(x => x.Phone, application.Phone)
                    .Set(x => x.Programme, application.Programme)
                    .Set(x => x.YearOfStudy, application.YearOfStudy)
                    .Set(x => x.GPA, application.GPA)
                    .Set(x => x.Motivation, application.Motivation)
                    .Set(x => x.PreviousExperience, application.PreviousExperience)
                    .Set(x => x.SubjectsInterested, application.SubjectsInterested)
                    .Set(x => x.Availability, application.Availability)
                    .Set(x => x.UpdatedAt, application.UpdatedAt)
                    .Update();

                var applicationDto = MapToDto(application);
                return new ApiResponse<TutorApplicationDto> 
                { 
                    Success = true, 
                    Data = applicationDto, 
                    Message = "Application updated successfully" 
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating tutor application");
                return new ApiResponse<TutorApplicationDto> 
                { 
                    Success = false, 
                    Message = "Failed to update tutor application" 
                };
            }
        }

        public async Task<ApiResponse<bool>> DeleteApplicationAsync(int applicationId, string userId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                // Check if application exists and belongs to user
                var existingApplication = await client
                    .From<TutorApplicationEntity>()
                    .Where(x => x.ApplicationId == applicationId && x.UserId == userId)
                    .Get();

                if (!existingApplication.Models.Any())
                {
                    return new ApiResponse<bool> 
                    { 
                        Success = false, 
                        Message = "Application not found or you don't have permission to delete it" 
                    };
                }

                var application = existingApplication.Models.First();

                // Only allow deletion if status is pending
                if (application.Status != "pending")
                {
                    return new ApiResponse<bool> 
                    { 
                        Success = false, 
                        Message = "Cannot delete application that has already been reviewed" 
                    };
                }

                // Delete transcript file if exists
                if (!string.IsNullOrEmpty(application.TranscriptUrl))
                {
                    await DeleteTranscriptAsync(applicationId);
                }

                // Delete application
                await client
                    .From<TutorApplicationEntity>()
                    .Where(x => x.ApplicationId == applicationId)
                    .Delete();

                return new ApiResponse<bool> 
                { 
                    Success = true, 
                    Data = true, 
                    Message = "Application deleted successfully" 
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting tutor application");
                return new ApiResponse<bool> 
                { 
                    Success = false, 
                    Message = "Failed to delete tutor application" 
                };
            }
        }

        public async Task<ApiResponse<TutorApplicationDto>> ReviewApplicationAsync(int applicationId, TutorApplicationReviewDto dto, string adminUserId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var application = await client
                    .From<TutorApplicationEntity>()
                    .Where(x => x.ApplicationId == applicationId)
                    .Get();

                if (!application.Models.Any())
                {
                    return new ApiResponse<TutorApplicationDto> 
                    { 
                        Success = false, 
                        Message = "Application not found" 
                    };
                }

                var app = application.Models.First();

                app.Status = dto.Status;
                app.AdminNotes = dto.AdminNotes;
                app.ReviewedBy = adminUserId;
                app.ReviewedAt = DateTime.UtcNow;
                app.UpdatedAt = DateTime.UtcNow;

                await client
                    .From<TutorApplicationEntity>()
                    .Where(x => x.ApplicationId == applicationId)
                    .Set(x => x.Status, app.Status)
                    .Set(x => x.AdminNotes, app.AdminNotes)
                    .Set(x => x.ReviewedBy, app.ReviewedBy)
                    .Set(x => x.ReviewedAt, app.ReviewedAt)
                    .Set(x => x.UpdatedAt, app.UpdatedAt)
                    .Update();

                var applicationDto = MapToDto(app);
                return new ApiResponse<TutorApplicationDto> 
                { 
                    Success = true, 
                    Data = applicationDto, 
                    Message = $"Application {dto.Status} successfully" 
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reviewing tutor application");
                return new ApiResponse<TutorApplicationDto> 
                { 
                    Success = false, 
                    Message = "Failed to review tutor application" 
                };
            }
        }

        public async Task<ApiResponse<string>> UploadTranscriptAsync(int applicationId, IFormFile transcriptFile)
        {
            try
            {
                // Validate file
                if (transcriptFile == null || transcriptFile.Length == 0)
                {
                    return new ApiResponse<string> 
                    { 
                        Success = false, 
                        Message = "No file provided" 
                    };
                }

                // Validate file type
                var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx" };
                var fileExtension = Path.GetExtension(transcriptFile.FileName).ToLowerInvariant();
                
                if (!allowedExtensions.Contains(fileExtension))
                {
                    return new ApiResponse<string> 
                    { 
                        Success = false, 
                        Message = "Invalid file type. Only PDF, JPG, PNG, DOC, and DOCX files are allowed" 
                    };
                }

                // Validate file size (max 10MB)
                if (transcriptFile.Length > 10 * 1024 * 1024)
                {
                    return new ApiResponse<string> 
                    { 
                        Success = false, 
                        Message = "File size too large. Maximum size is 10MB" 
                    };
                }

                // Create uploads directory if it doesn't exist
                var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads", "transcripts");
                if (!Directory.Exists(uploadsPath))
                {
                    Directory.CreateDirectory(uploadsPath);
                }

                // Generate unique filename
                var fileName = $"{applicationId}_{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(uploadsPath, fileName);

                // Save file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await transcriptFile.CopyToAsync(stream);
                }

                // Return relative URL
                var fileUrl = $"/uploads/transcripts/{fileName}";
                return new ApiResponse<string> 
                { 
                    Success = true, 
                    Data = fileUrl, 
                    Message = "Transcript uploaded successfully" 
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading transcript");
                return new ApiResponse<string> 
                { 
                    Success = false, 
                    Message = "Failed to upload transcript" 
                };
            }
        }

        public async Task<ApiResponse<bool>> DeleteTranscriptAsync(int applicationId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var application = await client
                    .From<TutorApplicationEntity>()
                    .Where(x => x.ApplicationId == applicationId)
                    .Get();

                if (!application.Models.Any())
                {
                    return new ApiResponse<bool> 
                    { 
                        Success = false, 
                        Message = "Application not found" 
                    };
                }

                var app = application.Models.First();

                if (string.IsNullOrEmpty(app.TranscriptUrl))
                {
                    return new ApiResponse<bool> 
                    { 
                        Success = true, 
                        Data = true, 
                        Message = "No transcript to delete" 
                    };
                }

                // Delete file from filesystem
                var fileName = Path.GetFileName(app.TranscriptUrl);
                var filePath = Path.Combine(_environment.WebRootPath, "uploads", "transcripts", fileName);
                
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                // Update database
                await client
                    .From<TutorApplicationEntity>()
                    .Where(x => x.ApplicationId == applicationId)
                    .Set(x => x.TranscriptUrl, (string?)null)
                    .Set(x => x.TranscriptFilename, (string?)null)
                    .Update();

                return new ApiResponse<bool> 
                { 
                    Success = true, 
                    Data = true, 
                    Message = "Transcript deleted successfully" 
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting transcript");
                return new ApiResponse<bool> 
                { 
                    Success = false, 
                    Message = "Failed to delete transcript" 
                };
            }
        }

        private static TutorApplicationDto MapToDto(TutorApplicationEntity entity)
        {
            return new TutorApplicationDto
            {
                ApplicationId = entity.ApplicationId,
                UserId = entity.UserId,
                FullName = entity.FullName,
                Email = entity.Email,
                Phone = entity.Phone,
                Programme = entity.Programme,
                YearOfStudy = entity.YearOfStudy,
                GPA = entity.GPA,
                TranscriptUrl = entity.TranscriptUrl,
                TranscriptFilename = entity.TranscriptFilename,
                Motivation = entity.Motivation,
                PreviousExperience = entity.PreviousExperience,
                SubjectsInterested = entity.SubjectsInterested,
                Availability = entity.Availability,
                Status = entity.Status,
                AdminNotes = entity.AdminNotes,
                ReviewedBy = entity.ReviewedBy,
                ReviewedAt = entity.ReviewedAt,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }
    }
}
