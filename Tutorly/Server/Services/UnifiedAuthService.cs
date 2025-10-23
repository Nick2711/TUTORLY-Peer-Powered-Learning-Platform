using Tutorly.Server.DatabaseModels;
using Tutorly.Shared;
using SupaClient = Supabase.Client;

namespace Tutorly.Server.Services
{
    public interface IUnifiedAuthService
    {
        Task<UnifiedSessionDto?> GetUserSessionAsync(string userId, string? email = null);
    }

    public class UnifiedAuthService : IUnifiedAuthService
    {
        private readonly SupaClient _supabase;
        private readonly ILogger<UnifiedAuthService> _logger;

        public UnifiedAuthService(SupaClient supabase, ILogger<UnifiedAuthService> logger)
        {
            _supabase = supabase;
            _logger = logger;
        }

        public async Task<UnifiedSessionDto?> GetUserSessionAsync(string userId, string? email = null)
        {
            try
            {
                _logger.LogInformation("Getting user session for userId: {UserId}", userId);

                // Create user info with available data
                var userInfo = new UnifiedUserDto
                {
                    Id = userId,
                    Email = email ?? "user@example.com",
                    CreatedAt = DateTime.UtcNow,
                    EmailConfirmedAt = DateTime.UtcNow,
                    LastSignInAt = DateTime.UtcNow
                };

                // Resolve role and profile (priority: student -> tutor -> admin)
                var (role, profile) = await ResolveUserRoleAndProfileAsync(userId);
                if (role == null || profile == null)
                {
                    _logger.LogWarning("No profile found for user: {UserId}", userId);
                    return null;
                }

                return new UnifiedSessionDto
                {
                    User = userInfo,
                    Role = role,
                    Profile = profile,
                    IsAuthenticated = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user session for userId: {UserId}", userId);
                return null;
            }
        }

        private async Task<(string? role, UnifiedProfileDto? profile)> ResolveUserRoleAndProfileAsync(string userId)
        {
            try
            {
                _logger.LogInformation("Resolving role and profile for userId: {UserId}", userId);

                // Check student_profiles first (priority 1)
                var studentProfile = await GetStudentProfileAsync(userId);
                if (studentProfile != null)
                {
                    _logger.LogInformation("Found student profile for userId: {UserId}", userId);
                    return ("student", MapStudentProfileToUnified(studentProfile));
                }

                // Check tutor_profiles (priority 2)
                var tutorProfile = await GetTutorProfileAsync(userId);
                if (tutorProfile != null)
                {
                    _logger.LogInformation("Found tutor profile for userId: {UserId}", userId);
                    return ("tutor", MapTutorProfileToUnified(tutorProfile));
                }

                // Check admins (priority 3)
                var adminProfile = await GetAdminProfileAsync(userId);
                if (adminProfile != null)
                {
                    _logger.LogInformation("Found admin profile for userId: {UserId}", userId);
                    return ("admin", MapAdminProfileToUnified(adminProfile));
                }

                _logger.LogWarning("No profile found for userId: {UserId}", userId);
                return (null, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving role and profile for userId: {UserId}", userId);
                return (null, null);
            }
        }

        private async Task<StudentProfileEntity?> GetStudentProfileAsync(string userId)
        {
            try
            {
                var result = await _supabase
                    .From<StudentProfileEntity>()
                    .Select("*")
                    .Get();

                return result.Models.FirstOrDefault(x => x.UserId == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching student profile for userId: {UserId}", userId);
                return null;
            }
        }

        private async Task<TutorProfileEntity?> GetTutorProfileAsync(string userId)
        {
            try
            {
                var result = await _supabase
                    .From<TutorProfileEntity>()
                    .Select("*")
                    .Get();

                return result.Models.FirstOrDefault(x => x.UserId == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching tutor profile for userId: {UserId}", userId);
                return null;
            }
        }

        private async Task<AdminProfileEntity?> GetAdminProfileAsync(string userId)
        {
            try
            {
                var result = await _supabase
                    .From<AdminProfileEntity>()
                    .Select("*")
                    .Get();

                return result.Models.FirstOrDefault(x => x.UserId == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching admin profile for userId: {UserId}", userId);
                return null;
            }
        }

        private static UnifiedProfileDto MapStudentProfileToUnified(StudentProfileEntity profile)
        {
            return new UnifiedProfileDto
            {
                Role = profile.Role,
                FullName = profile.FullName,
                AvatarUrl = profile.AvatarUrl,
                CreatedAt = profile.CreatedAt,
                StudentId = profile.StudentId,
                Programme = profile.Programme,
                YearOfStudy = profile.YearOfStudy
            };
        }

        private static UnifiedProfileDto MapTutorProfileToUnified(TutorProfileEntity profile)
        {
            return new UnifiedProfileDto
            {
                Role = profile.Role,
                FullName = profile.FullName,
                AvatarUrl = profile.AvatarUrl,
                CreatedAt = profile.CreatedAt,
                TutorId = profile.TutorId,
                Programme = profile.Programme,
                YearOfStudy = profile.YearOfStudy
            };
        }

        private static UnifiedProfileDto MapAdminProfileToUnified(AdminProfileEntity profile)
        {
            return new UnifiedProfileDto
            {
                Role = profile.Role,
                FullName = profile.FullName,
                CreatedAt = profile.CreatedAt,
                AdminId = profile.AdminId,
                ActiveAdmin = profile.ActiveAdmin
            };
        }
    }
}
