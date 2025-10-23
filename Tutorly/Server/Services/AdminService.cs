using Supabase;
using Tutorly.Server.DatabaseModels;
using Tutorly.Shared;
using Microsoft.Extensions.Options;
using Tutorly.Server.Helpers;

namespace Tutorly.Server.Services
{
    public class AdminService : IAdminService
    {
        private readonly Supabase.Client _supabaseClient;
        private readonly SupabaseSettings _supabaseSettings;
        private readonly ILogger<AdminService> _logger;
        private readonly ISupabaseAuthService _supabaseAuthService;
        private readonly IEmailNotificationService _emailService;

        public AdminService(
            Supabase.Client supabaseClient,
            IOptions<SupabaseSettings> supabaseSettings,
            ILogger<AdminService> logger,
            ISupabaseAuthService supabaseAuthService,
            IEmailNotificationService emailService)
        {
            _supabaseClient = supabaseClient;
            _supabaseSettings = supabaseSettings.Value;
            _logger = logger;
            _supabaseAuthService = supabaseAuthService;
            _emailService = emailService;
        }

        public async Task<ServiceResult<int>> GetTotalUsersCountAsync()
        {
            try
            {
                // First try to get count from Supabase auth.users table
                var authUsersCount = await GetAuthUsersCountAsync();
                if (authUsersCount.HasValue)
                {
                    return ServiceResult<int>.SuccessResult(authUsersCount.Value);
                }

                // Fallback: count from local tables
                var localCount = await GetLocalUsersCountAsync();
                return ServiceResult<int>.SuccessResult(localCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting total users count");
                return ServiceResult<int>.FailureResult("Failed to get total users count");
            }
        }

        public async Task<ServiceResult<AdminDashboardStats>> GetDashboardStatsAsync()
        {
            try
            {
                var stats = new AdminDashboardStats();

                // Get user counts
                var totalUsersResult = await GetTotalUsersCountAsync();
                if (totalUsersResult.Success)
                {
                    stats.TotalUsers = totalUsersResult.Data;
                }

                // Get student count
                var studentsResult = await _supabaseClient
                    .From<StudentProfileEntity>()
                    .Get();
                stats.TotalStudents = studentsResult?.Models?.Count ?? 0;

                // Get tutor count
                var tutorsResult = await _supabaseClient
                    .From<TutorProfileEntity>()
                    .Get();
                stats.TotalTutors = tutorsResult?.Models?.Count ?? 0;

                // Get admin count
                var adminsResult = await _supabaseClient
                    .From<AdminProfileEntity>()
                    .Get();
                stats.TotalAdmins = adminsResult?.Models?.Count ?? 0;

                // TODO: Implement flagged content count when forum moderation is ready
                stats.FlaggedContent = 0;

                // TODO: Implement pending approvals count when tutor approval system is ready
                stats.PendingApprovals = 0;

                // Get modules count
                var modulesResult = await _supabaseClient
                    .From<ModuleEntity>()
                    .Get();
                stats.TotalModules = modulesResult?.Models?.Count ?? 0;

                return ServiceResult<AdminDashboardStats>.SuccessResult(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard stats");
                return ServiceResult<AdminDashboardStats>.FailureResult("Failed to get dashboard stats");
            }
        }

        private Task<int?> GetAuthUsersCountAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_supabaseSettings.ServiceRoleKey))
                {
                    _logger.LogWarning("ServiceRoleKey is not configured. Cannot fetch auth users count.");
                    return Task.FromResult<int?>(null);
                }

                // For now, we'll skip the auth.users count and use local tables
                // This would require a custom RPC function in Supabase
                // TODO: Implement custom RPC function in Supabase to count auth.users
                return Task.FromResult<int?>(null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get auth users count, falling back to local tables");
                return Task.FromResult<int?>(null);
            }
        }

        private async Task<int> GetLocalUsersCountAsync()
        {
            try
            {
                // Count students
                var studentsResult = await _supabaseClient
                    .From<StudentProfileEntity>()
                    .Get();
                var studentCount = studentsResult?.Models?.Count ?? 0;
                _logger.LogInformation("DEBUG: GetLocalUsersCountAsync - Found {Count} students", studentCount);

                // Count tutors
                var tutorsResult = await _supabaseClient
                    .From<TutorProfileEntity>()
                    .Get();
                var tutorCount = tutorsResult?.Models?.Count ?? 0;
                _logger.LogInformation("DEBUG: GetLocalUsersCountAsync - Found {Count} tutors", tutorCount);

                // Count admins
                var adminsResult = await _supabaseClient
                    .From<AdminProfileEntity>()
                    .Get();
                var adminCount = adminsResult?.Models?.Count ?? 0;
                _logger.LogInformation("DEBUG: GetLocalUsersCountAsync - Found {Count} admins", adminCount);

                var totalCount = studentCount + tutorCount + adminCount;
                _logger.LogInformation("DEBUG: GetLocalUsersCountAsync - Total users: {TotalCount}", totalCount);
                return totalCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting local users");
                return 0;
            }
        }

        public async Task<ServiceResult<List<UserDto>>> GetAllUsersAsync()
        {
            try
            {
                var users = new List<UserDto>();

                // Get all students
                var studentsResult = await _supabaseClient
                    .From<StudentProfileEntity>()
                    .Get();

                _logger.LogInformation("DEBUG: Found {Count} students in database", studentsResult?.Models?.Count ?? 0);
                if (studentsResult?.Models != null)
                {
                    foreach (var student in studentsResult.Models)
                    {
                        _logger.LogInformation("DEBUG: Student - StudentId: {StudentId}, UserId: {UserId}, FullName: {FullName}",
                            student.StudentId, student.UserId, student.FullName);
                    }
                }

                foreach (var student in studentsResult?.Models ?? new List<StudentProfileEntity>())
                {
                    var email = await _supabaseAuthService.GetUserEmailByUserIdAsync(student.UserId);
                    users.Add(new UserDto
                    {
                        UserId = student.UserId,
                        FullName = student.FullName,
                        Email = email ?? "Email not available",
                        Role = "Student",
                        Status = student.Status ?? "Active", // Default to Active if Status column doesn't exist yet
                        JoinedDate = student.CreatedAt,
                        AvatarUrl = student.AvatarUrl
                    });
                }

                // Get all tutors
                var tutorsResult = await _supabaseClient
                    .From<TutorProfileEntity>()
                    .Get();

                foreach (var tutor in tutorsResult?.Models ?? new List<TutorProfileEntity>())
                {
                    var email = await _supabaseAuthService.GetUserEmailByUserIdAsync(tutor.UserId);
                    users.Add(new UserDto
                    {
                        UserId = tutor.UserId,
                        FullName = tutor.FullName,
                        Email = email ?? "Email not available",
                        Role = "Tutor",
                        Status = tutor.Status ?? "Active", // Default to Active if Status column doesn't exist yet
                        JoinedDate = tutor.CreatedAt,
                        AvatarUrl = tutor.AvatarUrl
                    });
                }

                // Get all admins
                var adminsResult = await _supabaseClient
                    .From<AdminProfileEntity>()
                    .Get();

                foreach (var admin in adminsResult?.Models ?? new List<AdminProfileEntity>())
                {
                    var email = await _supabaseAuthService.GetUserEmailByUserIdAsync(admin.UserId);
                    users.Add(new UserDto
                    {
                        UserId = admin.UserId,
                        FullName = admin.FullName,
                        Email = email ?? "Email not available",
                        Role = "Admin",
                        Status = admin.Status ?? "Active", // Default to Active if Status column doesn't exist yet
                        JoinedDate = admin.CreatedAt,
                        AvatarUrl = null
                    });
                }

                return ServiceResult<List<UserDto>>.SuccessResult(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all users");
                return ServiceResult<List<UserDto>>.FailureResult("Failed to get users");
            }
        }

        public async Task<ServiceResult> UpdateUserStatusAsync(string userId, string status)
        {
            try
            {
                // Check if user is a student
                var studentResult = await _supabaseClient
                    .From<StudentProfileEntity>()
                    .Where(s => s.UserId == userId)
                    .Get();

                if (studentResult?.Models?.Any() == true)
                {
                    var student = studentResult.Models.First();
                    student.Status = status;
                    await _supabaseClient
                        .From<StudentProfileEntity>()
                        .Where(s => s.StudentId == student.StudentId)
                        .Set(s => s.Status, status)
                        .Update();
                }
                else
                {
                    // Check if user is a tutor
                    var tutorResult = await _supabaseClient
                        .From<TutorProfileEntity>()
                        .Where(t => t.UserId == userId)
                        .Get();

                    if (tutorResult?.Models?.Any() == true)
                    {
                        var tutor = tutorResult.Models.First();
                        await _supabaseClient
                            .From<TutorProfileEntity>()
                            .Where(t => t.TutorId == tutor.TutorId)
                            .Set(t => t.Status, status)
                            .Update();
                    }
                    else
                    {
                        // Check if user is an admin
                        var adminResult = await _supabaseClient
                            .From<AdminProfileEntity>()
                            .Where(a => a.UserId == userId)
                            .Get();

                        if (adminResult?.Models?.Any() == true)
                        {
                            var admin = adminResult.Models.First();
                            await _supabaseClient
                                .From<AdminProfileEntity>()
                                .Where(a => a.AdminId == admin.AdminId)
                                .Set(a => a.Status, status)
                                .Update();
                        }
                        else
                        {
                            return ServiceResult.FailureResult("User not found");
                        }
                    }
                }

                return ServiceResult.SuccessResult($"User status updated to {status}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user status for userId {UserId}", userId);
                return ServiceResult.FailureResult("Failed to update user status");
            }
        }

        public async Task<ServiceResult> WarnUserAsync(string userId, string adminId, string message)
        {
            try
            {
                // Create warning record
                var warning = new UserWarningsEntity
                {
                    UserId = userId,
                    WarnedByAdminId = adminId,
                    WarningMessage = message,
                    CreatedAt = DateTime.UtcNow
                };

                await _supabaseClient
                    .From<UserWarningsEntity>()
                    .Insert(warning);

                // Get user email for notification
                var userEmail = await _supabaseAuthService.GetUserEmailByUserIdAsync(userId);
                if (!string.IsNullOrEmpty(userEmail))
                {
                    // Get user name
                    var userName = await GetUserNameAsync(userId);

                    // Send warning email
                    await SendWarningEmailAsync(userEmail, userName, message);
                }

                return ServiceResult.SuccessResult("Warning sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending warning to userId {UserId}", userId);
                return ServiceResult.FailureResult("Failed to send warning");
            }
        }

        public async Task<ServiceResult<List<UserWarningDto>>> GetUserWarningsAsync(string userId)
        {
            try
            {
                var warningsResult = await _supabaseClient
                    .From<UserWarningsEntity>()
                    .Where(w => w.UserId == userId)
                    .Order(w => w.CreatedAt, Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get();

                var warnings = warningsResult?.Models?.Select(w => new UserWarningDto
                {
                    WarningId = w.WarningId,
                    UserId = w.UserId,
                    WarnedByAdminId = w.WarnedByAdminId,
                    WarningMessage = w.WarningMessage,
                    CreatedAt = w.CreatedAt
                }).ToList() ?? new List<UserWarningDto>();

                return ServiceResult<List<UserWarningDto>>.SuccessResult(warnings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting warnings for userId {UserId}", userId);
                return ServiceResult<List<UserWarningDto>>.FailureResult("Failed to get user warnings");
            }
        }

        private async Task<string> GetUserNameAsync(string userId)
        {
            try
            {
                // Try student first
                var studentResult = await _supabaseClient
                    .From<StudentProfileEntity>()
                    .Where(s => s.UserId == userId)
                    .Get();

                if (studentResult?.Models?.Any() == true)
                {
                    return studentResult.Models.First().FullName;
                }

                // Try tutor
                var tutorResult = await _supabaseClient
                    .From<TutorProfileEntity>()
                    .Where(t => t.UserId == userId)
                    .Get();

                if (tutorResult?.Models?.Any() == true)
                {
                    return tutorResult.Models.First().FullName;
                }

                // Try admin
                var adminResult = await _supabaseClient
                    .From<AdminProfileEntity>()
                    .Where(a => a.UserId == userId)
                    .Get();

                if (adminResult?.Models?.Any() == true)
                {
                    return adminResult.Models.First().FullName;
                }

                return "User";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user name for userId {UserId}", userId);
                return "User";
            }
        }

        private async Task SendWarningEmailAsync(string userEmail, string userName, string message)
        {
            try
            {
                var subject = "Warning: Account Activity Notice";
                var body = $@"
Dear {userName},

{message}

Please review our terms of service and community guidelines. Continued violations may result in further action.

Best regards,
Tutorly Admin Team";

                await _emailService.SendEmailAsync(userEmail, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending warning email to {Email}", userEmail);
            }
        }

        public async Task<ServiceResult<string>> AddUserAsync(AddUserRequest request)
        {
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password) ||
                    string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Role))
                {
                    return ServiceResult<string>.FailureResult("All fields are required");
                }

                if (!IsValidEmail(request.Email))
                {
                    return ServiceResult<string>.FailureResult("Invalid email format");
                }

                if (request.Password.Length < 6)
                {
                    return ServiceResult<string>.FailureResult("Password must be at least 6 characters long");
                }

                if (!new[] { "Student", "Tutor", "Admin" }.Contains(request.Role))
                {
                    return ServiceResult<string>.FailureResult("Invalid role. Must be Student, Tutor, or Admin");
                }

                // Check for phantom records BEFORE creating auth user
                if (request.Role.ToLower() == "student" || request.Role.ToLower() == "tutor")
                {
                    var extractedId = ExtractStudentIdFromEmail(request.Email);
                    _logger.LogInformation("Checking for phantom records with ID: {Id} before creating auth user", extractedId);

                    try
                    {
                        // Check for phantom student records
                        if (request.Role.ToLower() == "student")
                        {
                            _logger.LogInformation("Checking for existing student records with StudentId: {StudentId}", extractedId);

                            // First, let's check what's actually in the database
                            var allStudentsResponse = await _supabaseClient.From<StudentProfileEntity>()
                                .Get();

                            _logger.LogInformation("All students in database - Count: {Count}", allStudentsResponse.Models?.Count ?? 0);
                            foreach (var student in allStudentsResponse.Models ?? new List<StudentProfileEntity>())
                            {
                                _logger.LogInformation("Existing student - StudentId: {StudentId}, UserId: {UserId}, FullName: '{FullName}'",
                                    student.StudentId, student.UserId, student.FullName);
                            }

                            var existingStudentResponse = await _supabaseClient.From<StudentProfileEntity>()
                                .Where(x => x.StudentId == extractedId)
                                .Get();

                            _logger.LogInformation("Student query result for StudentId {StudentId} - Count: {Count}", extractedId, existingStudentResponse.Models?.Count ?? 0);

                            if (existingStudentResponse.Models?.Any() == true)
                            {
                                var foundStudent = existingStudentResponse.Models.First();
                                _logger.LogInformation("Found existing student record - StudentId: {StudentId}, UserId: {UserId}, FullName: '{FullName}'",
                                    foundStudent.StudentId, foundStudent.UserId, foundStudent.FullName);

                                // Check if this is a phantom record (any record with this StudentId should be considered phantom since we're trying to create a new one)
                                _logger.LogWarning("Found existing student record with StudentId {StudentId}, treating as phantom and cleaning up", extractedId);

                                // Delete the existing record
                                await _supabaseClient.From<StudentProfileEntity>()
                                    .Where(x => x.StudentId == extractedId)
                                    .Delete();

                                _logger.LogInformation("Deleted existing student record with StudentId {StudentId}", extractedId);

                                // Try to delete the associated auth user
                                try
                                {
                                    await _supabaseAuthService.DeleteUserAsync(foundStudent.UserId);
                                    _logger.LogInformation("Cleaned up associated auth user {UserId}", foundStudent.UserId);
                                }
                                catch (Exception deleteAuthEx)
                                {
                                    _logger.LogWarning(deleteAuthEx, "Could not delete associated auth user {UserId}", foundStudent.UserId);
                                }

                                _logger.LogInformation("Existing student record cleaned up successfully");
                            }
                            else
                            {
                                _logger.LogInformation("No existing student record found with StudentId {StudentId}", extractedId);

                                // Try a more aggressive cleanup - use raw SQL to force delete any record with this StudentId
                                _logger.LogWarning("Attempting aggressive cleanup for StudentId {StudentId} using raw SQL", extractedId);
                                try
                                {
                                    // Use raw SQL to delete any record with this StudentId
                                    var deleteSql = $"DELETE FROM student_profiles WHERE student_id = {extractedId}";
                                    _logger.LogInformation("Executing raw SQL: {Sql}", deleteSql);

                                    // Try using Supabase RPC to execute raw SQL
                                    var deleteResult = await _supabaseClient.Rpc("exec_sql", new Dictionary<string, object> { { "sql", deleteSql } });
                                    _logger.LogInformation("Raw SQL delete completed for StudentId {StudentId}", extractedId);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Raw SQL delete failed for StudentId {StudentId}, trying alternative approach", extractedId);

                                    // Alternative: Try to delete using the Supabase client with a different approach
                                    try
                                    {
                                        await _supabaseClient.From<StudentProfileEntity>()
                                            .Where(x => x.StudentId == extractedId)
                                            .Delete();
                                        _logger.LogInformation("Alternative delete completed for StudentId {StudentId}", extractedId);
                                    }
                                    catch (Exception ex2)
                                    {
                                        _logger.LogWarning(ex2, "Alternative delete also failed for StudentId {StudentId}", extractedId);
                                    }
                                }
                            }
                        }

                        // Check for phantom tutor records
                        if (request.Role.ToLower() == "tutor")
                        {
                            var existingTutorResponse = await _supabaseClient.From<TutorProfileEntity>()
                                .Where(x => x.TutorId == extractedId)
                                .Get();

                            if (existingTutorResponse.Models?.Any() == true)
                            {
                                var foundTutor = existingTutorResponse.Models.First();
                                _logger.LogInformation("Found existing tutor record - TutorId: {TutorId}, FullName: {FullName}",
                                    foundTutor.TutorId, foundTutor.FullName);

                                // Check if this is a phantom record
                                if (foundTutor.FullName == "User" || string.IsNullOrWhiteSpace(foundTutor.FullName))
                                {
                                    _logger.LogWarning("Found phantom tutor record, cleaning up before creating new user");

                                    // Delete the phantom record
                                    await _supabaseClient.From<TutorProfileEntity>()
                                        .Where(x => x.TutorId == extractedId)
                                        .Delete();

                                    // Try to delete the associated auth user
                                    try
                                    {
                                        await _supabaseAuthService.DeleteUserAsync(foundTutor.UserId);
                                        _logger.LogInformation("Cleaned up phantom auth user {UserId}", foundTutor.UserId);
                                    }
                                    catch (Exception deleteAuthEx)
                                    {
                                        _logger.LogWarning(deleteAuthEx, "Could not delete phantom auth user {UserId}", foundTutor.UserId);
                                    }

                                    _logger.LogInformation("Phantom tutor record cleaned up successfully");
                                }
                                else
                                {
                                    _logger.LogWarning("Tutor ID {TutorId} already exists with valid data: {FullName}", extractedId, foundTutor.FullName);
                                    return ServiceResult<string>.FailureResult($"Tutor ID {extractedId} already exists. Please use a different email address.");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error checking for phantom records with ID {Id}", extractedId);
                        // Continue anyway - the database will handle constraints
                    }
                }

                // Create user in Supabase Auth
                var authResult = await _supabaseAuthService.CreateUserAsync(request.Email, request.Password);
                if (!authResult.Success)
                {
                    // Check if it's an email exists error
                    if (authResult.Message.Contains("email_exists") || authResult.Message.Contains("already been registered"))
                    {
                        return ServiceResult<string>.FailureResult($"A user with email '{request.Email}' already exists. Please use a different email address.");
                    }
                    return ServiceResult<string>.FailureResult($"Failed to create user: {authResult.Message}");
                }

                var userId = authResult.Data;

                // Create profile based on role
                switch (request.Role.ToLower())
                {
                    case "student":
                        // Extract first 6 digits from email for StudentId
                        var studentId = ExtractStudentIdFromEmail(request.Email);
                        _logger.LogInformation("Creating student profile - Email: {Email}, Extracted StudentId: {StudentId}", request.Email, studentId);

                        var studentProfile = new StudentProfileEntity
                        {
                            StudentId = studentId,
                            UserId = userId,
                            FullName = request.FullName,
                            Programme = "BComp", // Use valid programme value
                            CreatedAt = DateTime.UtcNow
                            // Status = "Active", // TODO: Add this back after database schema update
                        };

                        _logger.LogInformation("StudentProfile before insert - StudentId: {StudentId}, UserId: {UserId}, FullName: {FullName}",
                            studentProfile.StudentId, studentProfile.UserId, studentProfile.FullName);

                        try
                        {
                            // First try to insert
                            await _supabaseClient.From<StudentProfileEntity>().Insert(studentProfile);
                            _logger.LogInformation("Successfully inserted student profile for StudentId: {StudentId}", studentId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to insert student profile for StudentId: {StudentId}, trying update approach", studentId);

                            // If insert fails due to duplicate key, try to update instead
                            if (ex.Message.Contains("duplicate key") || ex.Message.Contains("already exists"))
                            {
                                _logger.LogWarning("Duplicate key detected, attempting to update existing record for StudentId: {StudentId}", studentId);
                                try
                                {
                                    // Try to update the existing record
                                    await _supabaseClient.From<StudentProfileEntity>()
                                        .Where(x => x.StudentId == studentId)
                                        .Set(x => x.UserId, userId)
                                        .Set(x => x.FullName, request.FullName)
                                        .Set(x => x.Programme, "BComp")
                                        .Set(x => x.CreatedAt, DateTime.UtcNow)
                                        .Update();

                                    _logger.LogInformation("Successfully updated existing student profile for StudentId: {StudentId}", studentId);
                                }
                                catch (Exception updateEx)
                                {
                                    _logger.LogError(updateEx, "Failed to update existing student profile for StudentId: {StudentId}", studentId);

                                    // Try to delete the auth user since profile creation failed
                                    try
                                    {
                                        await _supabaseAuthService.DeleteUserAsync(userId);
                                        _logger.LogInformation("Deleted auth user {UserId} after profile creation failure", userId);
                                    }
                                    catch (Exception deleteEx)
                                    {
                                        _logger.LogError(deleteEx, "Failed to delete auth user {UserId} after profile creation failure", userId);
                                    }
                                    throw;
                                }
                            }
                            else
                            {
                                // Try to delete the auth user since profile creation failed
                                try
                                {
                                    await _supabaseAuthService.DeleteUserAsync(userId);
                                    _logger.LogInformation("Deleted auth user {UserId} after profile creation failure", userId);
                                }
                                catch (Exception deleteEx)
                                {
                                    _logger.LogError(deleteEx, "Failed to delete auth user {UserId} after profile creation failure", userId);
                                }
                                throw;
                            }
                        }
                        break;

                    case "tutor":
                        // Extract first 6 digits from email for TutorId
                        var tutorId = ExtractStudentIdFromEmail(request.Email);
                        _logger.LogInformation("Creating tutor profile - Email: {Email}, Extracted TutorId: {TutorId}", request.Email, tutorId);

                        var tutorProfile = new TutorProfileEntity
                        {
                            TutorId = tutorId,
                            UserId = userId,
                            FullName = request.FullName,
                            Programme = "BComp", // Use valid programme value
                            YearOfStudy = 1, // Set default year of study
                            Role = "tutor", // Set explicit role
                            CreatedAt = DateTime.UtcNow
                            // Status = "Active", // TODO: Add this back after database schema update
                        };
                        try
                        {
                            // First try to insert
                            await _supabaseClient.From<TutorProfileEntity>().Insert(tutorProfile);
                            _logger.LogInformation("Successfully inserted tutor profile for TutorId: {TutorId}", tutorId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to insert tutor profile for TutorId: {TutorId}, trying update approach", tutorId);

                            // If insert fails due to duplicate key, try to update instead
                            if (ex.Message.Contains("duplicate key") || ex.Message.Contains("already exists"))
                            {
                                _logger.LogWarning("Duplicate key detected, attempting to update existing record for TutorId: {TutorId}", tutorId);
                                try
                                {
                                    // Try to update the existing record
                                    await _supabaseClient.From<TutorProfileEntity>()
                                        .Where(x => x.TutorId == tutorId)
                                        .Set(x => x.UserId, userId)
                                        .Set(x => x.FullName, request.FullName)
                                        .Set(x => x.Programme, "BComp")
                                        .Set(x => x.YearOfStudy, 1)
                                        .Set(x => x.Role, "tutor")
                                        .Set(x => x.CreatedAt, DateTime.UtcNow)
                                        .Update();

                                    _logger.LogInformation("Successfully updated existing tutor profile for TutorId: {TutorId}", tutorId);
                                }
                                catch (Exception updateEx)
                                {
                                    _logger.LogError(updateEx, "Failed to update existing tutor profile for TutorId: {TutorId}", tutorId);

                                    // Try to delete the auth user since profile creation failed
                                    try
                                    {
                                        await _supabaseAuthService.DeleteUserAsync(userId);
                                        _logger.LogInformation("Deleted auth user {UserId} after profile creation failure", userId);
                                    }
                                    catch (Exception deleteEx)
                                    {
                                        _logger.LogError(deleteEx, "Failed to delete auth user {UserId} after profile creation failure", userId);
                                    }
                                    throw;
                                }
                            }
                            else
                            {
                                // Try to delete the auth user since profile creation failed
                                try
                                {
                                    await _supabaseAuthService.DeleteUserAsync(userId);
                                    _logger.LogInformation("Deleted auth user {UserId} after profile creation failure", userId);
                                }
                                catch (Exception deleteEx)
                                {
                                    _logger.LogError(deleteEx, "Failed to delete auth user {UserId} after profile creation failure", userId);
                                }
                                throw;
                            }
                        }
                        break;

                    case "admin":
                        var adminProfile = new AdminProfileEntity
                        {
                            UserId = userId,
                            FullName = request.FullName,
                            Role = "admin",
                            ActiveAdmin = true,
                            CreatedAt = DateTime.UtcNow
                            // Status = "Active", // TODO: Add this back after database schema update
                        };
                        await _supabaseClient.From<AdminProfileEntity>().Insert(adminProfile);
                        break;
                }

                _logger.LogInformation("Successfully created {Role} user: {Email}", request.Role, request.Email);
                return ServiceResult<string>.SuccessResult(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user with email {Email}", request.Email);
                return ServiceResult<string>.FailureResult("Failed to create user");
            }
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private int ExtractStudentIdFromEmail(string email)
        {
            _logger.LogInformation("Extracting StudentId from email: {Email}", email);

            // Extract first 6 digits from email (e.g., "601188@student.belgiumcampus.ac.za" -> 601188)
            var match = System.Text.RegularExpressions.Regex.Match(email, @"^(\d{6})");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int studentId))
            {
                _logger.LogInformation("Successfully extracted StudentId: {StudentId} from email: {Email}", studentId, email);
                return studentId;
            }

            // Fallback: generate a random 6-digit number if email doesn't start with digits
            var random = new Random();
            var fallbackId = random.Next(100000, 999999);
            _logger.LogWarning("Email {Email} doesn't start with 6 digits, using fallback StudentId: {StudentId}", email, fallbackId);
            return fallbackId;
        }

        public async Task<ServiceResult> DeleteUserAsync(string userId)
        {
            try
            {
                // First, delete the profile from the appropriate table
                var studentResult = await _supabaseClient
                    .From<StudentProfileEntity>()
                    .Where(s => s.UserId == userId)
                    .Get();

                if (studentResult?.Models?.Any() == true)
                {
                    var student = studentResult.Models.First();
                    await _supabaseClient
                        .From<StudentProfileEntity>()
                        .Where(s => s.StudentId == student.StudentId)
                        .Delete();
                }
                else
                {
                    // Check if user is a tutor
                    var tutorResult = await _supabaseClient
                        .From<TutorProfileEntity>()
                        .Where(t => t.UserId == userId)
                        .Get();

                    if (tutorResult?.Models?.Any() == true)
                    {
                        var tutor = tutorResult.Models.First();
                        await _supabaseClient
                            .From<TutorProfileEntity>()
                            .Where(t => t.TutorId == tutor.TutorId)
                            .Delete();
                    }
                    else
                    {
                        // Check if user is an admin
                        var adminResult = await _supabaseClient
                            .From<AdminProfileEntity>()
                            .Where(a => a.UserId == userId)
                            .Get();

                        if (adminResult?.Models?.Any() == true)
                        {
                            var admin = adminResult.Models.First();
                            await _supabaseClient
                                .From<AdminProfileEntity>()
                                .Where(a => a.AdminId == admin.AdminId)
                                .Delete();
                        }
                        else
                        {
                            return ServiceResult.FailureResult("User profile not found");
                        }
                    }
                }

                // Delete user warnings
                await _supabaseClient
                    .From<UserWarningsEntity>()
                    .Where(w => w.UserId == userId)
                    .Delete();

                // Finally, delete the user from Supabase Auth
                var authResult = await _supabaseAuthService.DeleteUserAsync(userId);
                if (!authResult.Success)
                {
                    _logger.LogWarning("Failed to delete auth user {UserId}: {Message}", userId, authResult.Message);
                    return ServiceResult.FailureResult($"Profile deleted but failed to delete auth user: {authResult.Message}");
                }

                _logger.LogInformation("Successfully deleted user {UserId}", userId);
                return ServiceResult.SuccessResult("User deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}", userId);
                return ServiceResult.FailureResult("Failed to delete user");
            }
        }
    }
}
