using Microsoft.AspNetCore.Components.WebAssembly.Server;
using Supabase;
using Supabase.Postgrest;
using Supabase.Postgrest.Models;
using Tutorly.Server.DatabaseModels;
using Tutorly.Server.Helpers;
using Tutorly.Shared;

namespace Tutorly.Server.Services
{
    public class ForumService : IForumService
    {
        private readonly ISupabaseClientFactory _supabaseFactory;
        private readonly IContentFilterService _contentFilter;
        private readonly IForumNotificationService _notificationService;
        private readonly IEmailNotificationService _emailNotificationService;
        private readonly ISupabaseAuthService _supabaseAuthService;
        private readonly ILogger<ForumService> _logger;

        public ForumService(
            ISupabaseClientFactory supabaseFactory,
            IContentFilterService contentFilter,
            IForumNotificationService notificationService,
            IEmailNotificationService emailNotificationService,
            ISupabaseAuthService supabaseAuthService,
            ILogger<ForumService> logger)
        {
            _supabaseFactory = supabaseFactory;
            _contentFilter = contentFilter;
            _notificationService = notificationService;
            _emailNotificationService = emailNotificationService;
            _supabaseAuthService = supabaseAuthService;
            _logger = logger;
        }

        #region Community Operations

        public async Task<ApiResponse<CommunityDto>> CreateCommunityAsync(CreateCommunityDto dto, string userId)
        {
            try
            {
                _logger.LogInformation($"CreateCommunityAsync: userId={userId}, communityName={dto.CommunityName}");

                var client = _supabaseFactory.CreateService();

                // Get user profile to verify role
                var profile = await GetUserProfileAsync(userId);
                if (profile == null)
                {
                    _logger.LogWarning($"CreateCommunityAsync: User profile not found for userId={userId}");
                    return new ApiResponse<CommunityDto> { Success = false, Message = "User profile not found" };
                }

                _logger.LogInformation($"CreateCommunityAsync: Found profile - Role={profile.Role}, StudentId={profile.StudentId}");

                // Only students can create communities
                if (profile.Role != "student")
                {
                    _logger.LogWarning($"CreateCommunityAsync: User is not a student. Role={profile.Role}");
                    return new ApiResponse<CommunityDto> { Success = false, Message = "Only students can create communities" };
                }

                // Check if user is banned
                var banCheck = await IsUserBannedAsync(userId);
                if (banCheck.Success && banCheck.Data)
                {
                    _logger.LogWarning($"Banned user {userId} attempted to create a community");
                    return new ApiResponse<CommunityDto> { Success = false, Message = "You are currently banned and cannot create communities. Please contact an administrator if you believe this is an error." };
                }
                else if (!banCheck.Success)
                {
                    _logger.LogError($"Ban check failed for user {userId}: {banCheck.Message}. Blocking community creation for security.");
                    return new ApiResponse<CommunityDto> { Success = false, Message = "Unable to verify user status. Please try again or contact support." };
                }

                // Check for profanity (warning only, not blocking for communities)
                var profanityCheck = await _contentFilter.CheckContentAsync(dto.CommunityName + " " + dto.CommunityDescription);
                if (!profanityCheck.IsClean)
                {
                    _logger.LogWarning($"CreateCommunityAsync: Profanity detected but allowing creation. Warnings: {string.Join(", ", profanityCheck.Warnings)}");
                }

                var entity = new ForumCommunityEntity
                {
                    CommunityName = dto.CommunityName,
                    CommunityDescription = dto.CommunityDescription,
                    CommunityType = dto.CommunityType ?? "course",
                    CreatedByStudentId = profile.StudentId ?? 0,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    ModuleId = dto.ModuleId
                };

                var response = await client
                    .From<ForumCommunityEntity>()
                    .Insert(entity);
                var result = response.Models.First();

                var communityDto = new CommunityDto
                {
                    CommunityId = result.CommunityId,
                    CommunityName = result.CommunityName,
                    CommunityDescription = result.CommunityDescription,
                    CommunityType = result.CommunityType,
                    CreatedByStudentId = result.CreatedByStudentId,
                    CreatedAt = result.CreatedAt,
                    IsActive = result.IsActive,
                    Members = 1, // Creator is the first member
                    Posts = 0,
                    Joined = true,
                    ModuleId = result.ModuleId
                };

                // Notify all clients about new community
                await _notificationService.NotifyCommunityUpdateAsync(communityDto);

                _logger.LogInformation($"CreateCommunityAsync: Successfully created community {result.CommunityId}");
                return new ApiResponse<CommunityDto> { Success = true, Data = communityDto };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating community: {ex.Message}");
                return new ApiResponse<CommunityDto> { Success = false, Message = $"Failed to create community: {ex.Message}" };
            }
        }

        public async Task<ApiResponse<List<CommunityDto>>> GetCommunitiesAsync(CommunityFilterDto filter, string? userId = null)
        {
            try
            {
                _logger.LogInformation($"GetCommunitiesAsync: Starting with userId={userId}, filter={filter.SearchQuery}");
                var client = _supabaseFactory.CreateService();

                var query = client
                    .From<ForumCommunityEntity>()
                    .Where(x => x.IsActive == true);

                if (!string.IsNullOrEmpty(filter.SearchQuery))
                {
                    query = query.Where(x => x.CommunityName.Contains(filter.SearchQuery) ||
                                           (x.CommunityDescription != null && x.CommunityDescription.Contains(filter.SearchQuery)));
                }

                if (!string.IsNullOrEmpty(filter.CommunityType))
                {
                    query = query.Where(x => x.CommunityType == filter.CommunityType);
                }

                // Apply sorting
                query = filter.SortBy switch
                {
                    "az" => query.Order(x => x.CommunityName, Constants.Ordering.Ascending),
                    "members" => query.Order(x => x.CommunityId, Constants.Ordering.Descending), // TODO: Add member count
                    _ => query.Order(x => x.CreatedAt, Constants.Ordering.Descending)
                };

                var communitiesResponse = await query.Get();
                _logger.LogInformation($"GetCommunitiesAsync: Found {communitiesResponse.Models.Count} communities");

                var communityDtos = new List<CommunityDto>();
                
                // Get user's memberships if userId is provided
                var userMemberships = new HashSet<int>();
                if (!string.IsNullOrEmpty(userId))
                {
                    var profile = await GetUserProfileAsync(userId);
                    if (profile != null)
                    {
                        try
                        {
                            var membershipsResponse = await client
                                .From<ForumCommunityMembershipEntity>()
                                .Where(x => (profile.StudentId.HasValue && x.StudentId == profile.StudentId.Value) ||
                                           (profile.TutorId.HasValue && x.TutorId == profile.TutorId.Value))
                                .Get();
                            
                            userMemberships = membershipsResponse.Models
                                .Where(m => m.CommunityId.HasValue)
                                .Select(m => m.CommunityId!.Value)
                                .ToHashSet();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error fetching memberships, falling back to student-only query");
                            // Fallback to student-only query if tutor_id column doesn't exist
                            if (profile.StudentId.HasValue)
                            {
                                var membershipsResponse = await client
                                    .From<ForumCommunityMembershipEntity>()
                                    .Where(x => x.StudentId == profile.StudentId.Value)
                                    .Get();
                                
                                userMemberships = membershipsResponse.Models
                                    .Where(m => m.CommunityId.HasValue)
                                    .Select(m => m.CommunityId!.Value)
                                    .ToHashSet();
                            }
                        }
                    }
                }

                foreach (var community in communitiesResponse.Models)
                {
                    // Check if user is a member of this community
                    var isJoined = userMemberships.Contains(community.CommunityId);
                    
                    communityDtos.Add(new CommunityDto
                    {
                        CommunityId = community.CommunityId,
                        CommunityName = community.CommunityName,
                        CommunityDescription = community.CommunityDescription,
                        CommunityType = community.CommunityType,
                        CreatedByStudentId = community.CreatedByStudentId,
                        CreatedAt = community.CreatedAt,
                        IsActive = community.IsActive,
                        Members = 0, // TODO: Calculate from memberships
                        Posts = 0,   // TODO: Calculate from posts
                        Joined = isJoined,
                        ModuleId = null // TODO: Set to community.ModuleId once database column is added
                    });
                }

                return new ApiResponse<List<CommunityDto>> { Success = true, Data = communityDtos };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting communities");
                return new ApiResponse<List<CommunityDto>> { Success = false, Message = "Failed to get communities" };
            }
        }

        public async Task<ApiResponse<CommunityDto>> GetCommunityByIdAsync(int communityId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var communityResponse = await client
                    .From<ForumCommunityEntity>()
                    .Where(x => x.CommunityId == communityId && x.IsActive == true)
                    .Get();

                var community = communityResponse.Models.FirstOrDefault();
                if (community == null)
                {
                    return new ApiResponse<CommunityDto> { Success = false, Message = "Community not found" };
                }

                var communityDto = new CommunityDto
                {
                    CommunityId = community.CommunityId,
                    CommunityName = community.CommunityName,
                    CommunityDescription = community.CommunityDescription,
                    CommunityType = community.CommunityType,
                    CreatedByStudentId = community.CreatedByStudentId,
                    CreatedAt = community.CreatedAt,
                    IsActive = community.IsActive,
                    Members = 0, // TODO: Calculate
                    Posts = 0,   // TODO: Calculate
                    Joined = false, // TODO: Check membership
                    ModuleId = community.ModuleId
                };

                return new ApiResponse<CommunityDto> { Success = true, Data = communityDto };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting community by ID");
                return new ApiResponse<CommunityDto> { Success = false, Message = "Failed to get community" };
            }
        }

        public async Task<ApiResponse<bool>> JoinCommunityAsync(int communityId, string userId)
        {
            try
            {
                _logger.LogInformation($"JoinCommunityAsync: Starting for communityId={communityId}, userId={userId}");
                var client = _supabaseFactory.CreateService();

                var profile = await GetUserProfileAsync(userId);
                if (profile == null)
                {
                    _logger.LogWarning($"JoinCommunityAsync: User profile not found for userId={userId}");
                    return new ApiResponse<bool> { Success = false, Message = "User profile not found" };
                }

                _logger.LogInformation($"JoinCommunityAsync: Found profile - Role={profile.Role}, StudentId={profile.StudentId}, TutorId={profile.TutorId}");

                // Check if user can join communities (students or tutors)
                if (profile.Role != "student" && profile.Role != "tutor")
                {
                    _logger.LogWarning($"JoinCommunityAsync: User {userId} has invalid role: {profile.Role}");
                    return new ApiResponse<bool> { Success = false, Message = "Only students and tutors can join communities" };
                }

                // Check if user has either StudentId or TutorId
                if (!profile.StudentId.HasValue && !profile.TutorId.HasValue)
                {
                    _logger.LogWarning($"JoinCommunityAsync: User {userId} has neither StudentId nor TutorId");
                    return new ApiResponse<bool> { Success = false, Message = "User profile is incomplete - missing student or tutor ID" };
                }

                // Check if already a member
                _logger.LogInformation($"JoinCommunityAsync: Checking existing membership for communityId={communityId}");
                
                var existingMembership = new List<ForumCommunityMembershipEntity>();
                
                if (profile.StudentId.HasValue)
                {
                    var studentMembership = await client
                        .From<ForumCommunityMembershipEntity>()
                        .Where(x => x.CommunityId == communityId && x.StudentId == profile.StudentId.Value)
                        .Get();
                    existingMembership.AddRange(studentMembership.Models);
                }
                
                if (profile.TutorId.HasValue)
                {
                    var tutorMembership = await client
                        .From<ForumCommunityMembershipEntity>()
                        .Where(x => x.CommunityId == communityId && x.TutorId == profile.TutorId.Value)
                        .Get();
                    existingMembership.AddRange(tutorMembership.Models);
                }

                _logger.LogInformation($"JoinCommunityAsync: Found {existingMembership.Count} existing memberships");

                if (existingMembership.Any())
                {
                    var userType = profile.StudentId.HasValue ? "Student" : "Tutor";
                    var profileId = profile.StudentId ?? profile.TutorId ?? 0;
                    _logger.LogInformation($"{userType} {profileId} is already a member of community {communityId}");
                    return new ApiResponse<bool> { Success = true, Data = true, Message = "Already a member" };
                }

                _logger.LogInformation($"JoinCommunityAsync: Creating new membership for communityId={communityId}");
                var membership = new ForumCommunityMembershipEntity
                {
                    CommunityId = communityId,
                    StudentId = profile.StudentId,
                    TutorId = profile.TutorId,
                    JoinedAt = DateTime.UtcNow
                };

                await client
                    .From<ForumCommunityMembershipEntity>()
                    .Insert(membership);

                _logger.LogInformation($"Student {profile.StudentId} joined community {communityId}");
                return new ApiResponse<bool> { Success = true, Data = true, Message = "Successfully joined community" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining community");
                return new ApiResponse<bool> { Success = false, Message = "Failed to join community" };
            }
        }

        public async Task<ApiResponse<bool>> LeaveCommunityAsync(int communityId, string userId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var profile = await GetUserProfileAsync(userId);
                if (profile == null || (profile.Role != "student" && profile.Role != "tutor"))
                {
                    return new ApiResponse<bool> { Success = false, Message = "Only students and tutors can leave communities" };
                }

                // Delete membership
                if (profile.StudentId.HasValue)
                {
                    await client
                        .From<ForumCommunityMembershipEntity>()
                        .Where(x => x.CommunityId == communityId && x.StudentId == profile.StudentId.Value)
                        .Delete();
                }
                
                if (profile.TutorId.HasValue)
                {
                    await client
                        .From<ForumCommunityMembershipEntity>()
                        .Where(x => x.CommunityId == communityId && x.TutorId == profile.TutorId.Value)
                        .Delete();
                }

                var userType = profile.StudentId.HasValue ? "Student" : "Tutor";
                var profileId = profile.StudentId ?? profile.TutorId ?? 0;
                _logger.LogInformation($"{userType} {profileId} left community {communityId}");
                return new ApiResponse<bool> { Success = true, Data = true, Message = "Successfully left community" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leaving community");
                return new ApiResponse<bool> { Success = false, Message = "Failed to leave community" };
            }
        }

        public async Task<ApiResponse<bool>> AutoFollowModuleCommunityAsync(int tutorId, int moduleId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                // Get module data first
                var module = await client
                    .From<ModuleEntity>()
                    .Where(x => x.ModuleId == moduleId)
                    .Get();

                if (!module.Models.Any())
                {
                    return new ApiResponse<bool> { Success = false, Message = "Module not found" };
                }

                var moduleData = module.Models.First();

                // Look for existing community by module name pattern
                var expectedCommunityName = $"{moduleData.ModuleCode} - {moduleData.ModuleName}";
                var existingCommunity = await client
                    .From<ForumCommunityEntity>()
                    .Where(x => x.CommunityName == expectedCommunityName && x.IsActive == true)
                    .Get();

                ForumCommunityEntity community;

                if (!existingCommunity.Models.Any())
                {

                    community = new ForumCommunityEntity
                    {
                        CommunityName = $"{moduleData.ModuleCode} - {moduleData.ModuleName}",
                        CommunityDescription = $"Community for {moduleData.ModuleCode}: {moduleData.ModuleDescription}",
                        CommunityType = "module",
                        CreatedByStudentId = null,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true,
                        ModuleId = null // TODO: Set to moduleId once database column is added
                    };

                    var response = await client
                        .From<ForumCommunityEntity>()
                        .Insert(community);
                    community = response.Models.First();

                    _logger.LogInformation($"AutoFollowModuleCommunityAsync: Created new community {community.CommunityId} for module {moduleId}");
                }
                else
                {
                    community = existingCommunity.Models.First();
                    _logger.LogInformation($"AutoFollowModuleCommunityAsync: Found existing community {community.CommunityId} for module {moduleId}");
                }

                var tutorProfile = await client
                    .From<TutorProfileEntity>()
                    .Where(x => x.TutorId == tutorId)
                    .Get();

                if (!tutorProfile.Models.Any())
                {
                    return new ApiResponse<bool> { Success = false, Message = "Tutor profile not found" };
                }

                var tutorData = tutorProfile.Models.First();

                // Check if tutor is already a member of this community
                var existingMembership = await client
                    .From<ForumCommunityMembershipEntity>()
                    .Where(x => x.CommunityId == community.CommunityId && x.TutorId == tutorId)
                    .Get();

                if (existingMembership.Models.Any())
                {
                    _logger.LogInformation($"AutoFollowModuleCommunityAsync: Tutor {tutorId} is already a member of community {community.CommunityId}");
                    return new ApiResponse<bool> { Success = true, Data = true, Message = "Already a member" };
                }

                // Add tutor as a member of the community
                var membership = new ForumCommunityMembershipEntity
                {
                    CommunityId = community.CommunityId,
                    TutorId = tutorId,
                    JoinedAt = DateTime.UtcNow
                };

                await client
                    .From<ForumCommunityMembershipEntity>()
                    .Insert(membership);

                _logger.LogInformation($"AutoFollowModuleCommunityAsync: Successfully added tutor {tutorId} to community {community.CommunityId} for module {moduleId}");
                return new ApiResponse<bool> { Success = true, Data = true, Message = "Successfully followed module community" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error auto-following module community");
                return new ApiResponse<bool> { Success = false, Message = "Failed to auto-follow module community" };
            }
        }

        public async Task<ApiResponse<bool>> AutoUnfollowModuleCommunityAsync(int tutorId, int moduleId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                // Find the community for this module
                var community = await client
                    .From<ForumCommunityEntity>()
                    .Where(x => x.ModuleId == moduleId && x.IsActive == true)
                    .Get();

                if (!community.Models.Any())
                {
                    return new ApiResponse<bool> { Success = true, Data = true, Message = "No community found for module" };
                }

                var communityData = community.Models.First();

                // Get tutor profile to get student ID
                var tutorProfile = await client
                    .From<TutorProfileEntity>()
                    .Where(x => x.TutorId == tutorId)
                    .Get();

                if (!tutorProfile.Models.Any())
                {
                    return new ApiResponse<bool> { Success = false, Message = "Tutor profile not found" };
                }

                var tutorData = tutorProfile.Models.First();

                // Remove tutor from the community
                await client
                    .From<ForumCommunityMembershipEntity>()
                    .Where(x => x.CommunityId == communityData.CommunityId && x.TutorId == tutorId)
                    .Delete();

                _logger.LogInformation($"AutoUnfollowModuleCommunityAsync: Successfully removed tutor {tutorId} from community {communityData.CommunityId} for module {moduleId}");
                return new ApiResponse<bool> { Success = true, Data = true, Message = "Successfully unfollowed module community" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error auto-unfollowing module community");
                return new ApiResponse<bool> { Success = false, Message = "Failed to auto-unfollow module community" };
            }
        }

        #endregion

        #region Thread Operations

        public async Task<ApiResponse<ThreadDto>> CreateThreadAsync(int communityId, CreateThreadDto dto, string userId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var profile = await GetUserProfileAsync(userId);
                if (profile == null || profile.Role != "student")
                {
                    return new ApiResponse<ThreadDto> { Success = false, Message = "Only students can create threads" };
                }

                // Check if user is banned
                var banCheck = await IsUserBannedAsync(userId);
                if (banCheck.Success && banCheck.Data)
                {
                    _logger.LogWarning($"Banned user {userId} attempted to create a thread");
                    return new ApiResponse<ThreadDto> { Success = false, Message = "You are currently banned and cannot create threads. Please contact an administrator if you believe this is an error." };
                }
                else if (!banCheck.Success)
                {
                    // If ban check fails block user to be safe
                    _logger.LogError($"Ban check failed for user {userId}: {banCheck.Message}. Blocking thread creation for security.");
                    return new ApiResponse<ThreadDto> { Success = false, Message = "Unable to verify user status. Please try again or contact support." };
                }

                // Check for profanity
                var profanityCheck = await _contentFilter.CheckContentAsync(dto.ThreadName + " " + dto.ThreadDescription);
                if (!profanityCheck.IsClean)
                {
                    return new ApiResponse<ThreadDto>
                    {
                        Success = false,
                        Message = "Content contains inappropriate language. Please revise your post.",
                        Errors = profanityCheck.Warnings
                    };
                }

                var entity = new ForumThreadEntity
                {
                    CommunityId = communityId,
                    ThreadName = dto.ThreadName,
                    ThreadDescription = dto.ThreadDescription,
                    CreatedByStudentId = profile.StudentId ?? 0,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                var response = await client
                    .From<ForumThreadEntity>()
                    .Insert(entity);
                var result = response.Models.First();

                var threadDto = new ThreadDto
                {
                    ThreadId = result.ThreadId,
                    CommunityId = result.CommunityId,
                    ThreadName = result.ThreadName,
                    ThreadDescription = result.ThreadDescription,
                    CreatedByStudentId = result.CreatedByStudentId,
                    CreatedAt = result.CreatedAt,
                    IsActive = result.IsActive,
                    Posts = 0 
                };

                return new ApiResponse<ThreadDto> { Success = true, Data = threadDto };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating thread");
                return new ApiResponse<ThreadDto> { Success = false, Message = "Failed to create thread" };
            }
        }

        public async Task<ApiResponse<List<ThreadDto>>> GetThreadsByCommunityAsync(int communityId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var threadsResponse = await client
                    .From<ForumThreadEntity>()
                    .Where(x => x.CommunityId == communityId && x.IsActive == true)
                    .Order(x => x.CreatedAt, Constants.Ordering.Descending)
                    .Get();

                var threadDtos = threadsResponse.Models.Select(t => new ThreadDto
                {
                    ThreadId = t.ThreadId,
                    CommunityId = t.CommunityId,
                    ThreadName = t.ThreadName,
                    ThreadDescription = t.ThreadDescription,
                    CreatedByStudentId = t.CreatedByStudentId,
                    CreatedAt = t.CreatedAt,
                    IsActive = t.IsActive,
                    Posts = 0 
                }).ToList();

                return new ApiResponse<List<ThreadDto>> { Success = true, Data = threadDtos };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting threads by community");
                return new ApiResponse<List<ThreadDto>> { Success = false, Message = "Failed to get threads" };
            }
        }

        public async Task<ApiResponse<ThreadDto>> GetThreadByIdAsync(int threadId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var thread = await client
                    .From<ForumThreadEntity>()
                    .Where(x => x.ThreadId == threadId && x.IsActive == true)
                    .Single();

                if (thread == null)
                {
                    return new ApiResponse<ThreadDto> { Success = false, Message = "Thread not found" };
                }

                var threadDto = new ThreadDto
                {
                    ThreadId = thread.ThreadId,
                    CommunityId = thread.CommunityId,
                    ThreadName = thread.ThreadName,
                    ThreadDescription = thread.ThreadDescription,
                    CreatedByStudentId = thread.CreatedByStudentId,
                    CreatedAt = thread.CreatedAt,
                    IsActive = thread.IsActive,
                    Posts = 0 
                };

                return new ApiResponse<ThreadDto> { Success = true, Data = threadDto };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting thread by ID");
                return new ApiResponse<ThreadDto> { Success = false, Message = "Failed to get thread" };
            }
        }

        #endregion

        #region Post Operations

        public async Task<ApiResponse<PostDto>> CreatePostAsync(int threadId, CreatePostDto dto, string userId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var profile = await GetUserProfileAsync(userId);
                if (profile == null || profile.Role != "student")
                {
                    return new ApiResponse<PostDto> { Success = false, Message = "Only students can create posts" };
                }

                // Check if user is banned
                var banCheck = await IsUserBannedAsync(userId);
                if (banCheck.Success && banCheck.Data)
                {
                    _logger.LogWarning($"Banned user {userId} attempted to create a post");
                    return new ApiResponse<PostDto> { Success = false, Message = "You are currently banned and cannot create posts. Please contact an administrator if you believe this is an error." };
                }
                else if (!banCheck.Success)
                {
                    _logger.LogError($"Ban check failed for user {userId}: {banCheck.Message}. Blocking post creation for security.");
                    return new ApiResponse<PostDto> { Success = false, Message = "Unable to verify user status. Please try again or contact support." };
                }

                // Check for profanity
                var profanityCheck = await _contentFilter.CheckContentAsync(dto.Title + " " + dto.Content);
                if (!profanityCheck.IsClean)
                {
                    return new ApiResponse<PostDto>
                    {
                        Success = false,
                        Message = "Content contains inappropriate language. Please revise your post.",
                        Errors = profanityCheck.Warnings
                    };
                }

                var entity = new ForumPostEntity
                {
                    ThreadId = threadId,
                    CreatedByStudentId = profile.StudentId ?? 0,
                    Title = dto.Title,
                    Content = dto.Content,
                    IsAnonymous = dto.IsAnonymous,
                    PostType = dto.PostType,
                    Tag = dto.Tag,
                    ModuleId = dto.ModuleId,
                    CreatedAt = DateTime.UtcNow
                };

                var response = await client
                    .From<ForumPostEntity>()
                    .Insert(entity);
                var result = response.Models.First();

                var postDto = new PostDto
                {
                    ForumPostsId = result.ForumPostsId,
                    CreatedByStudentId = result.CreatedByStudentId,
                    Title = result.Title,
                    Content = result.Content,
                    IsAnonymous = result.IsAnonymous,
                    CreatedAt = result.CreatedAt,
                    UpdatedAt = result.UpdatedAt,
                    ModuleId = result.ModuleId,
                    PostType = result.PostType,
                    Tag = result.Tag,
                    AuthorName = dto.IsAnonymous ? "Anonymous" : profile.FullName,
                    Votes = 0,
                    Comments = 0,
                    Saves = 0,
                    TimeAgo = GetTimeAgo(result.CreatedAt),
                    Summary = GetSummary(result.Content)
                };

                // Notify clients about new post (if we had community/thread info)
                // await _notificationService.NotifyNewPostAsync(postDto, communityId);

                // Send email notifications to community members
                try
                {
                    var threadResponse = await GetThreadByIdAsync(threadId);
                    if (threadResponse?.Success == true && threadResponse.Data != null)
                    {
                        var communityResponse = await GetCommunityByIdAsync(threadResponse.Data.CommunityId);
                        if (communityResponse?.Success == true && communityResponse.Data != null)
                        {
                            _logger.LogInformation($"Sending email notifications for new post in community {communityResponse.Data.CommunityName}");
                            await _emailNotificationService.NotifyNewPostInCommunityAsync(
                                communityResponse.Data.CommunityId,
                                postDto.Title,
                                postDto.AuthorName,
                                communityResponse.Data.CommunityName,
                                postDto.ForumPostsId
                            );
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending email notifications for new post");
                }

                return new ApiResponse<PostDto> { Success = true, Data = postDto };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating post");
                return new ApiResponse<PostDto> { Success = false, Message = "Failed to create post" };
            }
        }

        public async Task<ApiResponse<List<PostDto>>> GetPostsByThreadAsync(int threadId, PostFilterDto filter)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                _logger.LogInformation($"GetPostsByThreadAsync: Fetching posts for thread {threadId}");

                var allPostsResponse = await client
                    .From<ForumPostEntity>()
                    .Get();

                _logger.LogInformation($"GetPostsByThreadAsync: Retrieved {allPostsResponse.Models.Count} total posts");

                // Filter in memory to avoid Supabase client issues
                var query = allPostsResponse.Models
                    .Where(x => x.ThreadId == threadId && x.DeletedAt == null)
                    .AsQueryable();

                _logger.LogInformation($"GetPostsByThreadAsync: Filtered to {query.Count()} posts for thread {threadId}");

                if (!string.IsNullOrEmpty(filter.PostType))
                {
                    query = query.Where(x => x.PostType == filter.PostType);
                }

                if (!string.IsNullOrEmpty(filter.Tag))
                {
                    query = query.Where(x => x.Tag == filter.Tag);
                }

                if (filter.ModuleId.HasValue)
                {
                    query = query.Where(x => x.ModuleId == filter.ModuleId);
                }

                // Apply sorting
                var sortedPosts = filter.SortBy switch
                {
                    "new" => query.OrderByDescending(x => x.CreatedAt),
                    "top" => query.OrderByDescending(x => x.CreatedAt),
                    _ => query.OrderByDescending(x => x.CreatedAt) 
                };

                var postDtos = new List<PostDto>();
                foreach (var post in sortedPosts)
                {
                    var authorName = post.IsAnonymous ? "Anonymous" : "Student"; 
                    var voteCount = await CalculatePostVoteCount(post.ForumPostsId);
                    var commentCount = await CalculateResponseCount(post.ForumPostsId);

                    postDtos.Add(new PostDto
                    {
                        ForumPostsId = post.ForumPostsId,
                        CreatedByStudentId = post.CreatedByStudentId,
                        Title = post.Title,
                        Content = post.Content,
                        IsAnonymous = post.IsAnonymous,
                        CreatedAt = post.CreatedAt,
                        UpdatedAt = post.UpdatedAt,
                        ModuleId = post.ModuleId,
                        PostType = post.PostType,
                        Tag = post.Tag,
                        AuthorName = authorName,
                        Votes = voteCount,
                        Comments = commentCount,
                        Saves = 0, // TODO: Calculate
                        TimeAgo = GetTimeAgo(post.CreatedAt),
                        Summary = GetSummary(post.Content)
                    });
                }

                return new ApiResponse<List<PostDto>> { Success = true, Data = postDtos };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting posts by thread {threadId}: {ex.Message}");
                _logger.LogError($"Exception details: {ex.GetType().Name}");
                if (ex.InnerException != null)
                {
                    _logger.LogError($"Inner exception: {ex.InnerException.Message}");
                }
                return new ApiResponse<List<PostDto>> { Success = false, Message = $"Failed to get posts: {ex.Message}" };
            }
        }

        public async Task<ApiResponse<PostDto>> GetPostByIdAsync(int postId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var postResponse = await client
                    .From<ForumPostEntity>()
                    .Where(x => x.ForumPostsId == postId && x.DeletedAt == null)
                    .Get();

                var post = postResponse.Models.FirstOrDefault();
                if (post == null)
                {
                    return new ApiResponse<PostDto> { Success = false, Message = "Post not found" };
                }

                var authorName = post.IsAnonymous ? "Anonymous" : "Student"; // TODO: Get actual name

                var postDto = new PostDto
                {
                    ForumPostsId = post.ForumPostsId,
                    CreatedByStudentId = post.CreatedByStudentId,
                    Title = post.Title,
                    Content = post.Content,
                    IsAnonymous = post.IsAnonymous,
                    CreatedAt = post.CreatedAt,
                    UpdatedAt = post.UpdatedAt,
                    ModuleId = post.ModuleId,
                    PostType = post.PostType,
                    Tag = post.Tag,
                    AuthorName = authorName,
                    Votes = 0, // TODO: Calculate
                    Comments = 0, // TODO: Calculate
                    Saves = 0, // TODO: Calculate
                    TimeAgo = GetTimeAgo(post.CreatedAt),
                    Summary = GetSummary(post.Content)
                };

                return new ApiResponse<PostDto> { Success = true, Data = postDto };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting post by ID");
                return new ApiResponse<PostDto> { Success = false, Message = "Failed to get post" };
            }
        }

        public async Task<ApiResponse<PostDto>> UpdatePostAsync(int postId, CreatePostDto dto, string userId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var profile = await GetUserProfileAsync(userId);
                if (profile == null)
                {
                    return new ApiResponse<PostDto> { Success = false, Message = "User not found" };
                }

                var post = await client
                    .From<ForumPostEntity>()
                    .Where(x => x.ForumPostsId == postId && x.DeletedAt == null)
                    .Single();

                if (post == null)
                {
                    return new ApiResponse<PostDto> { Success = false, Message = "Post not found" };
                }

                // Check if user owns the post or is admin
                if (post.CreatedByStudentId != profile.StudentId && profile.Role != "admin")
                {
                    return new ApiResponse<PostDto> { Success = false, Message = "You can only edit your own posts" };
                }

                // Check for profanity
                var profanityCheck = await _contentFilter.CheckContentAsync(dto.Title + " " + dto.Content);
                if (!profanityCheck.IsClean)
                {
                    return new ApiResponse<PostDto>
                    {
                        Success = false,
                        Message = "Content contains inappropriate language. Please revise your post.",
                        Errors = profanityCheck.Warnings
                    };
                }

                post.Title = dto.Title;
                post.Content = dto.Content;
                post.IsAnonymous = dto.IsAnonymous;
                post.PostType = dto.PostType;
                post.Tag = dto.Tag;
                post.ModuleId = dto.ModuleId;
                post.UpdatedAt = DateTime.UtcNow;

                var updateResponse = await client
                    .From<ForumPostEntity>()
                    .Where(x => x.ForumPostsId == postId)
                    .Update(post);
                var result = updateResponse.Models.First();

                var postDto = new PostDto
                {
                    ForumPostsId = result.ForumPostsId,
                    CreatedByStudentId = result.CreatedByStudentId,
                    Title = result.Title,
                    Content = result.Content,
                    IsAnonymous = result.IsAnonymous,
                    CreatedAt = result.CreatedAt,
                    UpdatedAt = result.UpdatedAt,
                    ModuleId = result.ModuleId,
                    PostType = result.PostType,
                    Tag = result.Tag,
                    AuthorName = dto.IsAnonymous ? "Anonymous" : profile.FullName,
                    Votes = 0, // TODO: Calculate
                    Comments = 0, // TODO: Calculate
                    Saves = 0, // TODO: Calculate
                    TimeAgo = GetTimeAgo(result.CreatedAt),
                    Summary = GetSummary(result.Content)
                };

                return new ApiResponse<PostDto> { Success = true, Data = postDto };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating post");
                return new ApiResponse<PostDto> { Success = false, Message = "Failed to update post" };
            }
        }

        public async Task<ApiResponse<bool>> DeletePostAsync(int postId, string userId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var profile = await GetUserProfileAsync(userId);
                if (profile == null)
                {
                    return new ApiResponse<bool> { Success = false, Message = "User not found" };
                }

                var post = await client
                    .From<ForumPostEntity>()
                    .Where(x => x.ForumPostsId == postId && x.DeletedAt == null)
                    .Single();

                if (post == null)
                {
                    return new ApiResponse<bool> { Success = false, Message = "Post not found" };
                }

                // Check if user owns the post or is admin
                if (post.CreatedByStudentId != profile.StudentId && profile.Role != "admin")
                {
                    return new ApiResponse<bool> { Success = false, Message = "You can only delete your own posts" };
                }

                post.DeletedAt = DateTime.UtcNow;
                await client
                    .From<ForumPostEntity>()
                    .Where(x => x.ForumPostsId == postId)
                    .Update(post);

                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting post");
                return new ApiResponse<bool> { Success = false, Message = "Failed to delete post" };
            }
        }

        #endregion

        #region Response Operations

        public async Task<ApiResponse<ResponseDto>> CreateResponseAsync(int postId, CreateResponseDto dto, string userId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var profile = await GetUserProfileAsync(userId);
                if (profile == null || (profile.Role != "student" && profile.Role != "tutor" && profile.Role != "admin"))
                {
                    return new ApiResponse<ResponseDto> { Success = false, Message = "Only authenticated users can create responses" };
                }

                // Check if user is banned (except admins)
                if (profile.Role != "admin")
                {
                    var banCheck = await IsUserBannedAsync(userId);
                    if (banCheck.Success && banCheck.Data)
                    {
                        _logger.LogWarning($"Banned user {userId} attempted to create a response");
                        return new ApiResponse<ResponseDto> { Success = false, Message = "You are currently banned and cannot create responses. Please contact an administrator if you believe this is an error." };
                    }
                    else if (!banCheck.Success)
                    {
                        // If ban check fails, we should block the user to be safe
                        _logger.LogError($"Ban check failed for user {userId}: {banCheck.Message}. Blocking response creation for security.");
                        return new ApiResponse<ResponseDto> { Success = false, Message = "Unable to verify user status. Please try again or contact support." };
                    }
                }

                // Check for profanity
                var profanityCheck = await _contentFilter.CheckContentAsync(dto.Content);
                if (!profanityCheck.IsClean)
                {
                    return new ApiResponse<ResponseDto>
                    {
                        Success = false,
                        Message = "Content contains inappropriate language. Please revise your post.",
                        Errors = profanityCheck.Warnings
                    };
                }

                // Author mapping: students use CreatedByStudentId; tutors use CreatedByTutorId
                int? creatorStudentId = profile.StudentId;
                int? creatorTutorId = profile.TutorId;

                var entity = new ForumResponseEntity
                {
                    ForumPostsId = postId,
                    CreatedByStudentId = creatorStudentId,
                    CreatedByTutorId = profile.Role == "tutor" ? creatorTutorId : null,
                    Content = dto.Content,
                    MaterialsId = dto.MaterialsId,
                    CreatedAt = DateTime.UtcNow
                };

                // If a tutor is replying, mark as tutor-verified immediately
                if (profile.Role == "tutor" && profile.TutorId.HasValue)
                {
                    entity.IsTutorVerified = true;
                    entity.VerifiedByTutorId = profile.TutorId.Value;
                    entity.VerifiedAt = DateTime.UtcNow;
                }

                var response = await client
                    .From<ForumResponseEntity>()
                    .Insert(entity);
                var result = response.Models.First();

                var responseDto = new ResponseDto
                {
                    ForumResponsesId = result.ForumResponsesId,
                    ForumPostsId = result.ForumPostsId,
                    CreatedByStudentId = result.CreatedByStudentId,
                    Content = result.Content,
                    CreatedAt = result.CreatedAt,
                    UpdatedAt = result.UpdatedAt,
                    IsTutorVerified = result.IsTutorVerified,
                    VerifiedByTutorId = result.VerifiedByTutorId,
                    VerifiedAt = result.VerifiedAt,
                    AuthorName = profile.FullName,
                    Votes = 0, // TODO: Calculate
                    TimeAgo = GetTimeAgo(result.CreatedAt)
                };

                // Notify clients about new response
                await _notificationService.NotifyNewResponseAsync(responseDto, postId);

                // Send email notifications
                try
                {
                    // Get the post to find its author
                    var post = await client
                        .From<ForumPostEntity>()
                        .Where(x => x.ForumPostsId == postId)
                        .Single();

                    if (post != null && post.CreatedByStudentId.HasValue)
                    {
                        // Get post author's email
                        var postAuthorEmail = await GetStudentEmailAsync(post.CreatedByStudentId.Value);

                        if (!string.IsNullOrEmpty(postAuthorEmail))
                        {
                            // Check if this response is from a tutor
                            bool isTutor = profile.Role == "tutor";

                            if (isTutor)
                            {
                                // Notify post author about tutor answer
                                _logger.LogInformation($"Sending tutor answer notification to post author");
                                await _emailNotificationService.NotifyPostAuthorOfTutorAnswerAsync(
                                    postId,
                                    post.Title,
                                    profile.FullName,
                                    postAuthorEmail
                                );

                                // Notify community members about tutor answer
                                if (post.ThreadId.HasValue)
                                {
                                    var threadResponse = await GetThreadByIdAsync(post.ThreadId.Value);
                                    if (threadResponse?.Success == true && threadResponse.Data != null)
                                    {
                                        var communityResponse = await GetCommunityByIdAsync(threadResponse.Data.CommunityId);
                                        if (communityResponse?.Success == true && communityResponse.Data != null)
                                        {
                                            _logger.LogInformation($"Sending tutor answer notification to community members");
                                            await _emailNotificationService.NotifyCommunityMembersOfTutorAnswerAsync(
                                                communityResponse.Data.CommunityId,
                                                post.Title,
                                                profile.FullName,
                                                communityResponse.Data.CommunityName,
                                                postId
                                            );
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // Regular reply notification
                                _logger.LogInformation($"Sending reply notification to post author");
                                await _emailNotificationService.NotifyPostAuthorOfReplyAsync(
                                    postId,
                                    post.Title,
                                    profile.FullName,
                                    postAuthorEmail
                                );
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending email notifications for response");
                }

                return new ApiResponse<ResponseDto> { Success = true, Data = responseDto };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating response");
                return new ApiResponse<ResponseDto> { Success = false, Message = "Failed to create response" };
            }
        }

        public async Task<ApiResponse<List<ResponseDto>>> GetResponsesByPostAsync(int postId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var responsesResponse = await client
                    .From<ForumResponseEntity>()
                    .Where(x => x.ForumPostsId == postId)
                    .Order(x => x.CreatedAt, Constants.Ordering.Ascending)
                    .Get();

                var responseDtos = new List<ResponseDto>();
                foreach (var response in responsesResponse.Models)
                {
                    responseDtos.Add(new ResponseDto
                    {
                        ForumResponsesId = response.ForumResponsesId,
                        ForumPostsId = response.ForumPostsId,
                        CreatedByStudentId = response.CreatedByStudentId,
                        CreatedByTutorId = response.CreatedByTutorId,
                        Content = response.Content,
                        CreatedAt = response.CreatedAt,
                        UpdatedAt = response.UpdatedAt,
                        IsTutorVerified = response.IsTutorVerified,
                        VerifiedByTutorId = response.VerifiedByTutorId,
                        VerifiedAt = response.VerifiedAt,
                        AuthorName = "Student", // TODO: Get actual name
                        Votes = 0, // TODO: Calculate
                        TimeAgo = GetTimeAgo(response.CreatedAt)
                    });
                }

                return new ApiResponse<List<ResponseDto>> { Success = true, Data = responseDtos };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting responses by post");
                return new ApiResponse<List<ResponseDto>> { Success = false, Message = "Failed to get responses" };
            }
        }

        public async Task<ApiResponse<ResponseDto>> UpdateResponseAsync(int responseId, CreateResponseDto dto, string userId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var profile = await GetUserProfileAsync(userId);
                if (profile == null)
                {
                    return new ApiResponse<ResponseDto> { Success = false, Message = "User not found" };
                }

                var response = await client
                    .From<ForumResponseEntity>()
                    .Where(x => x.ForumResponsesId == responseId)
                    .Single();

                if (response == null)
                {
                    return new ApiResponse<ResponseDto> { Success = false, Message = "Response not found" };
                }

                // Check if user owns the response or is admin
                if ((response.CreatedByStudentId != profile.StudentId) && profile.Role != "admin")
                {
                    return new ApiResponse<ResponseDto> { Success = false, Message = "You can only edit your own responses" };
                }

                // Check for profanity
                var profanityCheck = await _contentFilter.CheckContentAsync(dto.Content);
                if (!profanityCheck.IsClean)
                {
                    return new ApiResponse<ResponseDto>
                    {
                        Success = false,
                        Message = "Content contains inappropriate language. Please revise your post.",
                        Errors = profanityCheck.Warnings
                    };
                }

                response.Content = dto.Content;
                response.MaterialsId = dto.MaterialsId;
                response.UpdatedAt = DateTime.UtcNow;

                var updateResponse = await client
                    .From<ForumResponseEntity>()
                    .Where(x => x.ForumResponsesId == responseId)
                    .Update(response);
                var result = updateResponse.Models.First();

                var responseDto = new ResponseDto
                {
                    ForumResponsesId = result.ForumResponsesId,
                    ForumPostsId = result.ForumPostsId,
                    CreatedByStudentId = result.CreatedByStudentId,
                    CreatedByTutorId = result.CreatedByTutorId,
                    Content = result.Content,
                    CreatedAt = result.CreatedAt,
                    UpdatedAt = result.UpdatedAt,
                    IsTutorVerified = result.IsTutorVerified,
                    VerifiedByTutorId = result.VerifiedByTutorId,
                    VerifiedAt = result.VerifiedAt,
                    AuthorName = profile.FullName,
                    Votes = 0, // TODO: Calculate
                    TimeAgo = GetTimeAgo(result.CreatedAt)
                };

                return new ApiResponse<ResponseDto> { Success = true, Data = responseDto };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating response");
                return new ApiResponse<ResponseDto> { Success = false, Message = "Failed to update response" };
            }
        }

        public async Task<ApiResponse<bool>> DeleteResponseAsync(int responseId, string userId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var profile = await GetUserProfileAsync(userId);
                if (profile == null)
                {
                    return new ApiResponse<bool> { Success = false, Message = "User not found" };
                }

                var response = await client
                    .From<ForumResponseEntity>()
                    .Where(x => x.ForumResponsesId == responseId)
                    .Single();

                if (response == null)
                {
                    return new ApiResponse<bool> { Success = false, Message = "Response not found" };
                }

                // Check if user owns the response or is admin
                if ((response.CreatedByStudentId != profile.StudentId) && profile.Role != "admin")
                {
                    return new ApiResponse<bool> { Success = false, Message = "You can only delete your own responses" };
                }

                await client
                    .From<ForumResponseEntity>()
                    .Where(x => x.ForumResponsesId == responseId)
                    .Delete();

                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting response");
                return new ApiResponse<bool> { Success = false, Message = "Failed to delete response" };
            }
        }

        #endregion

        #region Vote Operations

        public async Task<ApiResponse<VoteDto>> VoteOnResponseAsync(int responseId, CreateVoteDto dto, string userId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var profile = await GetUserProfileAsync(userId);
                if (profile == null || profile.Role != "student")
                {
                    return new ApiResponse<VoteDto> { Success = false, Message = "Only students can vote" };
                }

                // Check if user already voted
                var existingVoteResponse = await client
                    .From<ForumVoteEntity>()
                    .Where(x => x.ForumResponsesId == responseId && x.StudentId == profile.StudentId)
                    .Get();
                var existingVote = existingVoteResponse.Models.FirstOrDefault();

                if (existingVote != null)
                {
                    // Update existing vote
                    existingVote.VoteType = dto.VoteType;
                    var updateResponse = await client
                        .From<ForumVoteEntity>()
                        .Where(x => x.ForumVotesId == existingVote.ForumVotesId)
                        .Update(existingVote);
                    var result = updateResponse.Models.First();

                    var voteDto = new VoteDto
                    {
                        ForumVotesId = result.ForumVotesId,
                        ForumResponsesId = result.ForumResponsesId ?? 0,
                        StudentId = result.StudentId,
                        VoteType = result.VoteType,
                        CreatedAt = result.CreatedAt
                    };

                    // Notify clients about vote update
                    await _notificationService.NotifyVoteUpdateAsync(responseId, result.VoteType);

                    return new ApiResponse<VoteDto> { Success = true, Data = voteDto };
                }
                else
                {
                    // Create new vote
                    var entity = new ForumVoteEntity
                    {
                        ForumResponsesId = responseId,
                        StudentId = profile.StudentId ?? 0,
                        VoteType = dto.VoteType,
                        CreatedAt = DateTime.UtcNow
                    };

                    var insertResponse = await client
                        .From<ForumVoteEntity>()
                        .Insert(entity);
                    var result = insertResponse.Models.First();

                    var voteDto = new VoteDto
                    {
                        ForumVotesId = result.ForumVotesId,
                        ForumResponsesId = result.ForumResponsesId ?? 0,
                        StudentId = result.StudentId,
                        VoteType = result.VoteType,
                        CreatedAt = result.CreatedAt
                    };

                    // Notify clients about vote update
                    await _notificationService.NotifyVoteUpdateAsync(responseId, result.VoteType);

                    return new ApiResponse<VoteDto> { Success = true, Data = voteDto };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error voting on response");
                return new ApiResponse<VoteDto> { Success = false, Message = "Failed to vote" };
            }
        }

        public async Task<ApiResponse<VoteDto>> VoteOnPostAsync(int postId, CreateVoteDto dto, string userId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var profile = await GetUserProfileAsync(userId);
                if (profile == null || profile.Role != "student")
                {
                    return new ApiResponse<VoteDto> { Success = false, Message = "Only students can vote" };
                }

                // Check if user already voted on this post
                var existingVoteResponse = await client
                    .From<ForumVoteEntity>()
                    .Where(x => x.ForumPostsId == postId && x.StudentId == profile.StudentId)
                    .Get();
                var existingVote = existingVoteResponse.Models.FirstOrDefault();

                VoteDto voteDto;

                if (existingVote != null)
                {
                    // If clicking the same vote type, remove the vote
                    if (existingVote.VoteType == dto.VoteType)
                    {
                        await client
                            .From<ForumVoteEntity>()
                            .Where(x => x.ForumVotesId == existingVote.ForumVotesId)
                            .Delete();

                        voteDto = new VoteDto
                        {
                            ForumVotesId = 0,
                            ForumResponsesId = 0,
                            StudentId = profile.StudentId ?? 0,
                            VoteType = 0, // Vote removed
                            CreatedAt = DateTime.UtcNow
                        };
                    }
                    else
                    {
                        // Update to opposite vote
                        existingVote.VoteType = dto.VoteType;
                        var updateResponse = await client
                            .From<ForumVoteEntity>()
                            .Where(x => x.ForumVotesId == existingVote.ForumVotesId)
                            .Update(existingVote);
                        var result = updateResponse.Models.First();

                        voteDto = new VoteDto
                        {
                            ForumVotesId = result.ForumVotesId,
                            ForumResponsesId = result.ForumResponsesId ?? 0,
                            StudentId = result.StudentId,
                            VoteType = result.VoteType,
                            CreatedAt = result.CreatedAt
                        };
                    }
                }
                else
                {
                    // Create new vote
                    var entity = new ForumVoteEntity
                    {
                        ForumPostsId = postId,
                        ForumResponsesId = null, // Not a response vote
                        StudentId = profile.StudentId ?? 0,
                        VoteType = dto.VoteType,
                        CreatedAt = DateTime.UtcNow
                    };

                    var insertResponse = await client
                        .From<ForumVoteEntity>()
                        .Insert(entity);
                    var result = insertResponse.Models.First();

                    voteDto = new VoteDto
                    {
                        ForumVotesId = result.ForumVotesId,
                        ForumResponsesId = result.ForumResponsesId ?? 0,
                        StudentId = result.StudentId,
                        VoteType = result.VoteType,
                        CreatedAt = result.CreatedAt
                    };
                }

                // Calculate the total vote count and notify all clients
                var totalVoteCount = await CalculatePostVoteCount(postId);
                await _notificationService.NotifyPostVoteUpdateAsync(postId, totalVoteCount);

                return new ApiResponse<VoteDto> { Success = true, Data = voteDto };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error voting on post");
                return new ApiResponse<VoteDto> { Success = false, Message = "Failed to vote on post" };
            }
        }

        public async Task<ApiResponse<bool>> RemoveVoteAsync(int responseId, string userId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var profile = await GetUserProfileAsync(userId);
                if (profile == null)
                {
                    return new ApiResponse<bool> { Success = false, Message = "User not found" };
                }

                await client
                    .From<ForumVoteEntity>()
                    .Where(x => x.ForumResponsesId == responseId && x.StudentId == profile.StudentId)
                    .Delete();

                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing vote");
                return new ApiResponse<bool> { Success = false, Message = "Failed to remove vote" };
            }
        }

        public async Task<ApiResponse<int>> GetResponseVoteCountAsync(int responseId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var votesResponse = await client
                    .From<ForumVoteEntity>()
                    .Where(x => x.ForumResponsesId == responseId)
                    .Get();

                var totalVotes = votesResponse.Models.Sum(v => v.VoteType);

                return new ApiResponse<int> { Success = true, Data = totalVotes };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting vote count");
                return new ApiResponse<int> { Success = false, Message = "Failed to get vote count" };
            }
        }

        #endregion

        #region Utility Operations

        public async Task<ApiResponse<List<PostDto>>> GetRecentPostsAsync(PostFilterDto filter)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var query = client
                    .From<ForumPostEntity>()
                    .Where(x => x.DeletedAt == null);

                if (!string.IsNullOrEmpty(filter.PostType))
                {
                    query = query.Where(x => x.PostType == filter.PostType);
                }

                if (!string.IsNullOrEmpty(filter.Tag))
                {
                    query = query.Where(x => x.Tag == filter.Tag);
                }

                if (filter.ModuleId.HasValue)
                {
                    query = query.Where(x => x.ModuleId == filter.ModuleId);
                }

                query = query.Order(x => x.CreatedAt, Constants.Ordering.Descending);

                var postsResponse = await query.Limit(filter.PageSize).Get();

                var postDtos = new List<PostDto>();
                foreach (var post in postsResponse.Models)
                {
                    var authorName = post.IsAnonymous ? "Anonymous" : "Student"; // TODO: Get actual name
                    var voteCount = await CalculatePostVoteCount(post.ForumPostsId);
                    var commentCount = await CalculateResponseCount(post.ForumPostsId);

                    postDtos.Add(new PostDto
                    {
                        ForumPostsId = post.ForumPostsId,
                        CreatedByStudentId = post.CreatedByStudentId,
                        Title = post.Title,
                        Content = post.Content,
                        IsAnonymous = post.IsAnonymous,
                        CreatedAt = post.CreatedAt,
                        UpdatedAt = post.UpdatedAt,
                        ModuleId = post.ModuleId,
                        PostType = post.PostType,
                        Tag = post.Tag,
                        AuthorName = authorName,
                        Votes = voteCount,
                        Comments = commentCount,
                        Saves = 0, // TODO: Calculate
                        TimeAgo = GetTimeAgo(post.CreatedAt),
                        Summary = GetSummary(post.Content)
                    });
                }

                return new ApiResponse<List<PostDto>> { Success = true, Data = postDtos };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent posts");
                return new ApiResponse<List<PostDto>> { Success = false, Message = "Failed to get recent posts" };
            }
        }

        public async Task<ApiResponse<List<PostDto>>> GetTrendingPostsAsync()
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                // Get posts from the last 7 days
                var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);

                var postsResponse = await client
                    .From<ForumPostEntity>()
                    .Where(x => x.CreatedAt >= sevenDaysAgo)
                    .Get();

                var posts = postsResponse.Models;

                // Calculate activity score for each post
                var postScores = new List<(ForumPostEntity Post, double Score)>();

                foreach (var post in posts)
                {
                    // Calculate vote count
                    var voteCount = await CalculatePostVoteCount(post.ForumPostsId);

                    // Calculate response count
                    var responseCount = await CalculateResponseCount(post.ForumPostsId);

                    // Calculate recency score (more recent = higher score)
                    var hoursSinceCreation = (DateTime.UtcNow - post.CreatedAt).TotalHours;
                    var recencyScore = Math.Max(0, 168 - hoursSinceCreation) / 168.0; // 168 hours = 7 days

                    // Calculate overall activity score
                    // Formula: (votes * 2) + (responses * 3) + (recency * 10)
                    var activityScore = (voteCount * 2) + (responseCount * 3) + (recencyScore * 10);

                    postScores.Add((post, activityScore));
                }

                // Sort by activity score and take top 3
                var trendingPosts = postScores
                    .OrderByDescending(x => x.Score)
                    .Take(3)
                    .Select(x => x.Post)
                    .ToList();

                // Map to DTOs
                var postDtos = new List<PostDto>();
                foreach (var post in trendingPosts)
                {
                    var authorName = post.IsAnonymous ? "Anonymous" : "Student";
                    var voteCount = await CalculatePostVoteCount(post.ForumPostsId);
                    var commentCount = await CalculateResponseCount(post.ForumPostsId);

                    postDtos.Add(new PostDto
                    {
                        ForumPostsId = post.ForumPostsId,
                        CreatedByStudentId = post.CreatedByStudentId,
                        Title = post.Title,
                        Content = post.Content,
                        IsAnonymous = post.IsAnonymous,
                        CreatedAt = post.CreatedAt,
                        UpdatedAt = post.UpdatedAt,
                        ModuleId = post.ModuleId,
                        PostType = post.PostType,
                        Tag = post.Tag,
                        AuthorName = authorName,
                        Votes = voteCount,
                        Comments = commentCount,
                        Saves = 0,
                        TimeAgo = GetTimeAgo(post.CreatedAt),
                        Summary = GetSummary(post.Content)
                    });
                }

                return new ApiResponse<List<PostDto>> { Success = true, Data = postDtos };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting trending posts");
                return new ApiResponse<List<PostDto>> { Success = false, Message = "Failed to get trending posts" };
            }
        }

        public async Task<ApiResponse<List<CommunityDto>>> GetPopularCommunitiesAsync()
        {
            // TODO: Implement popular communities based on member count and activity
            return await GetCommunitiesAsync(new CommunityFilterDto { PageSize = 10 });
        }

        public async Task<ApiResponse<ForumMetricsDto>> GetForumMetricsAsync()
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                // Calculate Active Students (students who posted or responded in the last 24 hours)
                var oneDayAgo = DateTime.UtcNow.AddDays(-1);

                var recentPostsResponse = await client
                    .From<ForumPostEntity>()
                    .Where(x => x.CreatedAt >= oneDayAgo)
                    .Get();

                var recentResponsesResponse = await client
                    .From<ForumResponseEntity>()
                    .Where(x => x.CreatedAt >= oneDayAgo)
                    .Get();

                var postStudentIds = recentPostsResponse.Models
                    .Where(p => p.CreatedByStudentId.HasValue)
                    .Select(p => p.CreatedByStudentId!.Value);

                var responseStudentIds = recentResponsesResponse.Models
                    .Where(r => r.CreatedByStudentId.HasValue)
                    .Select(r => r.CreatedByStudentId!.Value);

                var activeStudentIds = postStudentIds
                    .Concat(responseStudentIds)
                    .Distinct()
                    .Count();

                // Calculate Posts This Week
                var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
                var postsThisWeekResponse = await client
                    .From<ForumPostEntity>()
                    .Where(x => x.CreatedAt >= sevenDaysAgo)
                    .Get();
                var postsThisWeek = postsThisWeekResponse.Models.Count;

                // Calculate Total Communities
                var communitiesResponse = await client
                    .From<ForumCommunityEntity>()
                    .Where(x => x.IsActive == true)
                    .Get();
                var totalCommunities = communitiesResponse.Models.Count;

                // Calculate Solved Questions (questions with tutor-verified responses)
                var verifiedResponsesResponse = await client
                    .From<ForumResponseEntity>()
                    .Where(x => x.IsTutorVerified == true)
                    .Get();

                // Get unique post IDs (questions) that have verified responses
                var solvedQuestions = verifiedResponsesResponse.Models
                    .Select(r => r.ForumPostsId)
                    .Distinct()
                    .Count();

                var metrics = new ForumMetricsDto
                {
                    ActiveStudents = activeStudentIds,
                    PostsThisWeek = postsThisWeek,
                    TotalCommunities = totalCommunities,
                    SolvedQuestions = solvedQuestions
                };

                return new ApiResponse<ForumMetricsDto> { Success = true, Data = metrics };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting forum metrics");
                return new ApiResponse<ForumMetricsDto> { Success = false, Message = "Failed to get forum metrics" };
            }
        }

        public Task<ApiResponse<bool>> SavePostAsync(int postId, string userId)
        {
            // TODO: Implement post saving functionality
            return Task.FromResult(new ApiResponse<bool> { Success = true, Data = true });
        }

        public Task<ApiResponse<bool>> UnsavePostAsync(int postId, string userId)
        {
            // TODO: Implement post unsaving functionality
            return Task.FromResult(new ApiResponse<bool> { Success = true, Data = true });
        }

        public Task<ApiResponse<List<PostDto>>> GetSavedPostsAsync(string userId)
        {
            // TODO: Implement getting saved posts
            return Task.FromResult(new ApiResponse<List<PostDto>> { Success = true, Data = new List<PostDto>() });
        }

        #endregion

        #region Helper Methods


        private async Task<UnifiedProfileDto?> GetUserProfileAsync(string userId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                // Try to get student profile first
                var studentProfileResponse = await client
                    .From<StudentProfileEntity>()
                    .Where(x => x.UserId == userId)
                    .Get();

                if (studentProfileResponse.Models.Any())
                {
                    var studentProfile = studentProfileResponse.Models.First();
                    return new UnifiedProfileDto
                    {
                        Role = studentProfile.Role,
                        FullName = studentProfile.FullName,
                        StudentId = studentProfile.StudentId,
                        Programme = studentProfile.Programme,
                        YearOfStudy = studentProfile.YearOfStudy,
                        AvatarUrl = studentProfile.AvatarUrl,
                        CreatedAt = studentProfile.CreatedAt
                    };
                }

                // Try to get tutor profile
                var tutorProfileResponse = await client
                    .From<TutorProfileEntity>()
                    .Where(x => x.UserId == userId)
                    .Get();

                if (tutorProfileResponse.Models.Any())
                {
                    var tutorProfile = tutorProfileResponse.Models.First();
                    return new UnifiedProfileDto
                    {
                        Role = tutorProfile.Role,
                        FullName = tutorProfile.FullName,
                        TutorId = tutorProfile.TutorId,
                        Programme = tutorProfile.Programme,
                        YearOfStudy = tutorProfile.YearOfStudy,
                        AvatarUrl = tutorProfile.AvatarUrl,
                        CreatedAt = tutorProfile.CreatedAt
                    };
                }

                // Try to get admin profile
                var adminProfileResponse = await client
                    .From<AdminProfileEntity>()
                    .Where(x => x.UserId == userId)
                    .Get();

                if (adminProfileResponse.Models.Any())
                {
                    var adminProfile = adminProfileResponse.Models.First();
                    return new UnifiedProfileDto
                    {
                        Role = adminProfile.Role,
                        FullName = adminProfile.FullName,
                        AdminId = adminProfile.AdminId,
                        ActiveAdmin = adminProfile.ActiveAdmin,
                        CreatedAt = adminProfile.CreatedAt
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user profile");
                return null;
            }
        }

        private async Task<int> CalculatePostVoteCount(int postId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();
                var votesResponse = await client
                    .From<ForumVoteEntity>()
                    .Where(x => x.ForumPostsId == postId)
                    .Get();

                var votes = votesResponse.Models;
                var totalVotes = votes.Sum(v => v.VoteType);
                return totalVotes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error calculating vote count for post {postId}");
                return 0;
            }
        }

        private async Task<int> CalculateResponseCount(int postId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();
                var responsesResponse = await client
                    .From<ForumResponseEntity>()
                    .Where(x => x.ForumPostsId == postId)
                    .Get();

                return responsesResponse.Models.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error calculating response count for post {postId}");
                return 0;
            }
        }

        private static string GetTimeAgo(DateTime dateTime)
        {
            var timeSpan = DateTime.UtcNow - dateTime;

            if (timeSpan.TotalMinutes < 1)
                return "just now";
            if (timeSpan.TotalMinutes < 60)
                return $"{(int)timeSpan.TotalMinutes}m ago";
            if (timeSpan.TotalHours < 24)
                return $"{(int)timeSpan.TotalHours}h ago";
            if (timeSpan.TotalDays < 7)
                return $"{(int)timeSpan.TotalDays}d ago";
            if (timeSpan.TotalDays < 30)
                return $"{(int)(timeSpan.TotalDays / 7)}w ago";
            if (timeSpan.TotalDays < 365)
                return $"{(int)(timeSpan.TotalDays / 30)}mo ago";

            return $"{(int)(timeSpan.TotalDays / 365)}y ago";
        }

        private static string GetSummary(string content)
        {
            if (string.IsNullOrEmpty(content))
                return string.Empty;

            if (content.Length <= 240)
                return content;

            return content.Substring(0, 240) + "…";
        }

        private async Task<string?> GetStudentEmailAsync(int studentId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                // Get user_id from student_profiles
                var studentResponse = await client
                    .From<StudentProfileEntity>()
                    .Where(x => x.StudentId == studentId)
                    .Single();

                if (studentResponse == null || string.IsNullOrEmpty(studentResponse.UserId))
                {
                    _logger.LogWarning($"No student profile found for studentId {studentId}");
                    return null;
                }

                // Get email from Supabase Auth API
                var email = await _supabaseAuthService.GetUserEmailByUserIdAsync(studentResponse.UserId);

                if (string.IsNullOrEmpty(email))
                {
                    _logger.LogWarning($"No email found for userId {studentResponse.UserId}");
                }

                return email;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting email for student {studentId}");
                return null;
            }
        }

        #endregion

        #region Report Operations

        public async Task<ApiResponse<bool>> CreateReportAsync(CreateReportDto dto, string userId, string reportType, int reportedItemId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                // tutor or student
                var profile = await GetUserProfileAsync(userId);
                if (profile == null)
                {
                    return new ApiResponse<bool> { Success = false, Message = "User not found" };
                }

                var report = new ForumReportEntity
                {
                    ReportedByUserId = userId,
                    ReportedByTutorId = profile.TutorId,
                    ReportedByStudentId = profile.StudentId,
                    ReportType = reportType,
                    ReportedItemId = reportedItemId,
                    Reason = dto.Reason,
                    Details = dto.Details,
                    Severity = dto.Severity,
                    Status = "open",
                    CreatedAt = DateTime.UtcNow
                };

                await client
                    .From<ForumReportEntity>()
                    .Insert(report);

                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating report: {Message}", ex.Message);
                return new ApiResponse<bool> { Success = false, Message = $"Failed to create report: {ex.Message}" };
            }
        }

        public async Task<ApiResponse<List<ForumReportDto>>> GetAllReportsAsync()
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var reportsResponse = await client
                    .From<ForumReportEntity>()
                    .Order(x => x.CreatedAt, Constants.Ordering.Descending)
                    .Get();

                var reportDtos = new List<ForumReportDto>();
                foreach (var report in reportsResponse.Models)
                {
                    string? reportedContent = null;
                    string? reportedBy = null;
                    string? itemTitle = null;

                    if (report.ReportType == "post")
                    {
                        var postResponse = await client
                            .From<ForumPostEntity>()
                            .Where(x => x.ForumPostsId == report.ReportedItemId)
                            .Get();
                        var post = postResponse.Models.FirstOrDefault();
                        
                        if (post != null)
                        {
                            reportedContent = post.Content;
                            itemTitle = post.Title;
                            // Get the actual user ID who created the post
                            reportedBy = post.CreatedByStudentId?.ToString() ?? "Unknown";
                        }
                    }
                    else if (report.ReportType == "response")
                    {
                        var responseResponse = await client
                            .From<ForumResponseEntity>()
                            .Where(x => x.ForumResponsesId == report.ReportedItemId)
                            .Get();
                        var response = responseResponse.Models.FirstOrDefault();
                        
                        if (response != null)
                        {
                            reportedContent = response.Content;
                            // Get the user id who created the response
                            reportedBy = response.CreatedByStudentId.ToString() ?? response.CreatedByTutorId.ToString() ?? "Unknown";
                        }
                    }

                    reportDtos.Add(new ForumReportDto
                    {
                        ReportId = report.ReportId,
                        ReporterName = report.ReporterName,
                        ReportType = report.ReportType,
                        ReportedItemId = report.ReportedItemId,
                        Reason = report.Reason,
                        Details = report.Details,
                        Severity = report.Severity,
                        Status = report.Status,
                        CreatedAt = report.CreatedAt,
                        ResolvedAt = report.ResolvedAt,
                        ResolvedByUserId = report.ResolvedByUserId,
                        ResolutionNotes = report.ResolutionNotes,
                        ReportedContent = reportedContent,
                        ReportedBy = reportedBy,
                        ItemTitle = itemTitle
                    });
                }

                return new ApiResponse<List<ForumReportDto>> { Success = true, Data = reportDtos };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting reports");
                return new ApiResponse<List<ForumReportDto>> { Success = false, Message = "Failed to get reports" };
            }
        }

        public async Task<ApiResponse<bool>> UpdateReportStatusAsync(int reportId, UpdateReportStatusDto dto, string userId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var reportResponse = await client
                    .From<ForumReportEntity>()
                    .Where(x => x.ReportId == reportId)
                    .Get();
                var report = reportResponse.Models.FirstOrDefault();

                if (report == null)
                {
                    return new ApiResponse<bool> { Success = false, Message = "Report not found" };
                }

                report.Status = dto.Status;
                report.ResolutionNotes = dto.ResolutionNotes;
                report.ResolvedByUserId = userId;
                report.ResolvedAt = DateTime.UtcNow;

                await client
                    .From<ForumReportEntity>()
                    .Where(x => x.ReportId == reportId)
                    .Update(report);

                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating report status");
                return new ApiResponse<bool> { Success = false, Message = "Failed to update report status" };
            }
        }

        #endregion

        #region Warning Operations

        public async Task<ApiResponse<bool>> CreateWarningAsync(CreateWarningDto dto, string adminUserId)
        {
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(dto.UserId))
                {
                    return new ApiResponse<bool> { Success = false, Message = "User ID is required" };
                }

                if (string.IsNullOrWhiteSpace(dto.WarningMessage))
                {
                    return new ApiResponse<bool> { Success = false, Message = "Warning message is required" };
                }

                var client = _supabaseFactory.CreateService();

                var warning = new UserWarningEntity
                {
                    UserId = dto.UserId,
                    WarnedByAdminId = adminUserId,
                    WarningMessage = dto.WarningMessage,
                    CreatedAt = DateTime.UtcNow
                };

                await client
                    .From<UserWarningEntity>()
                    .Insert(warning);

                // Update profile status to "Warned"
                await UpdateUserProfileStatusAsync(dto.UserId, "Warned");

                // Check if user should be auto-banned (3+ warnings)
                var autoBanResult = await CheckAndAutoBanUserAsync(dto.UserId);
                if (autoBanResult.Success && autoBanResult.Data)
                {
                    _logger.LogInformation("User {UserId} was automatically banned due to 3+ warnings", dto.UserId);
                }

                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating warning: {Message}", ex.Message);
                return new ApiResponse<bool> { Success = false, Message = $"Failed to create warning: {ex.Message}" };
            }
        }

        public async Task<ApiResponse<List<WarningDto>>> GetWarningsByUserIdAsync(string userId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var warningsResponse = await client
                    .From<UserWarningEntity>()
                    .Where(x => x.UserId == userId)
                    .Order(x => x.CreatedAt, Constants.Ordering.Descending)
                    .Get();

                var warningDtos = new List<WarningDto>();
                foreach (var warning in warningsResponse.Models)
                {
                    // Get admin name
                    string? adminName = null;
                    try
                    {
                        var adminProfile = await GetAdminProfileAsync(warning.WarnedByAdminId);
                        adminName = adminProfile?.FullName ?? "Unknown Admin";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not get admin name for warning {WarningId}", warning.WarningId);
                        adminName = "Unknown Admin";
                    }

                    // Get user name
                    string? userName = null;
                    try
                    {
                        var userProfile = await GetUserProfileAsync(warning.UserId);
                        if (userProfile != null)
                        {
                            userName = userProfile.StudentId.HasValue ? "Student" : 
                                      userProfile.TutorId.HasValue ? "Tutor" : "User";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not get user name for warning {WarningId}", warning.WarningId);
                        userName = "Unknown User";
                    }

                    warningDtos.Add(new WarningDto
                    {
                        WarningId = warning.WarningId,
                        UserId = warning.UserId,
                        WarnedByAdminId = warning.WarnedByAdminId,
                        WarningMessage = warning.WarningMessage,
                        CreatedAt = warning.CreatedAt,
                        AdminName = adminName,
                        UserName = userName
                    });
                }

                return new ApiResponse<List<WarningDto>> { Success = true, Data = warningDtos };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting warnings for user {UserId}", userId);
                return new ApiResponse<List<WarningDto>> { Success = false, Message = "Failed to get warnings" };
            }
        }

        public async Task<ApiResponse<List<WarningDto>>> GetAllWarningsAsync()
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var warningsResponse = await client
                    .From<UserWarningEntity>()
                    .Order(x => x.CreatedAt, Constants.Ordering.Descending)
                    .Get();

                var warningDtos = new List<WarningDto>();
                foreach (var warning in warningsResponse.Models)
                {
                    // Get admin name
                    string? adminName = null;
                    try
                    {
                        var adminProfile = await GetAdminProfileAsync(warning.WarnedByAdminId);
                        adminName = adminProfile?.FullName ?? "Unknown Admin";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not get admin name for warning {WarningId}", warning.WarningId);
                        adminName = "Unknown Admin";
                    }

                    // Get user name
                    string? userName = null;
                    try
                    {
                        var userProfile = await GetUserProfileAsync(warning.UserId);
                        if (userProfile != null)
                        {
                            userName = userProfile.StudentId.HasValue ? "Student" : 
                                      userProfile.TutorId.HasValue ? "Tutor" : "User";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not get user name for warning {WarningId}", warning.WarningId);
                        userName = "Unknown User";
                    }

                    warningDtos.Add(new WarningDto
                    {
                        WarningId = warning.WarningId,
                        UserId = warning.UserId,
                        WarnedByAdminId = warning.WarnedByAdminId,
                        WarningMessage = warning.WarningMessage,
                        CreatedAt = warning.CreatedAt,
                        AdminName = adminName,
                        UserName = userName
                    });
                }

                return new ApiResponse<List<WarningDto>> { Success = true, Data = warningDtos };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all warnings");
                return new ApiResponse<List<WarningDto>> { Success = false, Message = "Failed to get warnings" };
            }
        }

        #endregion

        #region Ban Operations

        public async Task<ApiResponse<bool>> CreateBanAsync(CreateBanDto dto, string adminUserId)
        {
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(dto.UserId))
                {
                    return new ApiResponse<bool> { Success = false, Message = "User ID is required" };
                }

                if (string.IsNullOrWhiteSpace(dto.BanReason))
                {
                    return new ApiResponse<bool> { Success = false, Message = "Ban reason is required" };
                }

                var client = _supabaseFactory.CreateService();

                var existingBansResponse = await client
                    .From<UserBanEntity>()
                    .Where(x => x.UserId == dto.UserId && x.IsActive)
                    .Get();

                foreach (var existingBan in existingBansResponse.Models)
                {
                    existingBan.IsActive = false;
                    existingBan.UnbannedAt = DateTime.UtcNow;
                    existingBan.UnbannedByAdminId = adminUserId;
                    existingBan.UnbanReason = "Replaced by new ban";

                    await client
                        .From<UserBanEntity>()
                        .Update(existingBan);
                }

                // Create new ban
                var ban = new UserBanEntity
                {
                    UserId = dto.UserId,
                    BannedByAdminId = adminUserId,
                    BanReason = dto.BanReason,
                    BanType = dto.BanType,
                    BannedAt = DateTime.UtcNow,
                    ExpiresAt = dto.ExpiresAt,
                    IsActive = true
                };

                await client
                    .From<UserBanEntity>()
                    .Insert(ban);

                // Update profile status to "Banned"
                await UpdateUserProfileStatusAsync(dto.UserId, "Banned");

                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating ban: {Message}", ex.Message);
                return new ApiResponse<bool> { Success = false, Message = $"Failed to create ban: {ex.Message}" };
            }
        }

        public async Task<ApiResponse<bool>> UnbanUserAsync(int banId, UnbanUserDto dto, string adminUserId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var banResponse = await client
                    .From<UserBanEntity>()
                    .Where(x => x.BanId == banId)
                    .Get();

                var ban = banResponse.Models.FirstOrDefault();
                if (ban == null)
                {
                    return new ApiResponse<bool> { Success = false, Message = "Ban not found" };
                }

                ban.IsActive = false;
                ban.UnbannedAt = DateTime.UtcNow;
                ban.UnbannedByAdminId = adminUserId;
                ban.UnbanReason = dto.UnbanReason;

                await client
                    .From<UserBanEntity>()
                    .Update(ban);

                // Restore profile status to "Active"
                await UpdateUserProfileStatusAsync(ban.UserId, "Active");

                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unbanning user: {Message}", ex.Message);
                return new ApiResponse<bool> { Success = false, Message = $"Failed to unban user: {ex.Message}" };
            }
        }

        public async Task<ApiResponse<List<BanDto>>> GetBansByUserIdAsync(string userId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var bansResponse = await client
                    .From<UserBanEntity>()
                    .Where(x => x.UserId == userId)
                    .Order(x => x.BannedAt, Constants.Ordering.Descending)
                    .Get();

                var banDtos = new List<BanDto>();
                foreach (var ban in bansResponse.Models)
                {
                    // Get admin name
                    string? adminName = null;
                    try
                    {
                        var adminProfile = await GetAdminProfileAsync(ban.BannedByAdminId);
                        adminName = adminProfile?.FullName ?? "Unknown Admin";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not get admin name for ban {BanId}", ban.BanId);
                        adminName = "Unknown Admin";
                    }

                    // Get user name
                    string? userName = null;
                    try
                    {
                        var userProfile = await GetUserProfileAsync(ban.UserId);
                        if (userProfile != null)
                        {
                            userName = userProfile.StudentId.HasValue ? "Student" : 
                                      userProfile.TutorId.HasValue ? "Tutor" : "User";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not get user name for ban {BanId}", ban.BanId);
                        userName = "Unknown User";
                    }

                    banDtos.Add(new BanDto
                    {
                        BanId = ban.BanId,
                        UserId = ban.UserId,
                        BannedByAdminId = ban.BannedByAdminId,
                        BanReason = ban.BanReason,
                        BanType = ban.BanType,
                        BannedAt = ban.BannedAt,
                        ExpiresAt = ban.ExpiresAt,
                        IsActive = ban.IsActive,
                        UnbannedAt = ban.UnbannedAt,
                        UnbannedByAdminId = ban.UnbannedByAdminId,
                        UnbanReason = ban.UnbanReason,
                        AdminName = adminName,
                        UserName = userName,
                        IsExpired = ban.IsExpired
                    });
                }

                return new ApiResponse<List<BanDto>> { Success = true, Data = banDtos };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting bans for user {UserId}", userId);
                return new ApiResponse<List<BanDto>> { Success = false, Message = "Failed to get bans" };
            }
        }

        public async Task<ApiResponse<List<BanDto>>> GetAllBansAsync()
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var bansResponse = await client
                    .From<UserBanEntity>()
                    .Order(x => x.BannedAt, Constants.Ordering.Descending)
                    .Get();

                var banDtos = new List<BanDto>();
                foreach (var ban in bansResponse.Models)
                {
                    // Get admin name
                    string? adminName = null;
                    try
                    {
                        var adminProfile = await GetAdminProfileAsync(ban.BannedByAdminId);
                        adminName = adminProfile?.FullName ?? "Unknown Admin";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not get admin name for ban {BanId}", ban.BanId);
                        adminName = "Unknown Admin";
                    }

                    // Get user name
                    string? userName = null;
                    try
                    {
                        var userProfile = await GetUserProfileAsync(ban.UserId);
                        if (userProfile != null)
                        {
                            userName = userProfile.StudentId.HasValue ? "Student" : 
                                      userProfile.TutorId.HasValue ? "Tutor" : "User";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not get user name for ban {BanId}", ban.BanId);
                        userName = "Unknown User";
                    }

                    banDtos.Add(new BanDto
                    {
                        BanId = ban.BanId,
                        UserId = ban.UserId,
                        BannedByAdminId = ban.BannedByAdminId,
                        BanReason = ban.BanReason,
                        BanType = ban.BanType,
                        BannedAt = ban.BannedAt,
                        ExpiresAt = ban.ExpiresAt,
                        IsActive = ban.IsActive,
                        UnbannedAt = ban.UnbannedAt,
                        UnbannedByAdminId = ban.UnbannedByAdminId,
                        UnbanReason = ban.UnbanReason,
                        AdminName = adminName,
                        UserName = userName,
                        IsExpired = ban.IsExpired
                    });
                }

                return new ApiResponse<List<BanDto>> { Success = true, Data = banDtos };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all bans");
                return new ApiResponse<List<BanDto>> { Success = false, Message = "Failed to get bans" };
            }
        }

        public async Task<ApiResponse<bool>> IsUserBannedAsync(string userId)
        {
            try
            {
                // Enhanced validation
                if (string.IsNullOrWhiteSpace(userId))
                {
                    _logger.LogWarning("IsUserBannedAsync called with null or empty userId");
                    return new ApiResponse<bool> { Success = false, Message = "User ID is required" };
                }

                // Additional validation for problematic values
                if (userId == "null" || userId == "undefined" || userId.Contains("null"))
                {
                    _logger.LogWarning("IsUserBannedAsync called with invalid userId: {UserId}", userId);
                    return new ApiResponse<bool> { Success = false, Message = "Invalid User ID" };
                }

                var client = _supabaseFactory.CreateService();
                if (client == null)
                {
                    _logger.LogError("Supabase client is null in IsUserBannedAsync for user {UserId}", userId);
                    return new ApiResponse<bool> { Success = false, Message = "Database connection failed" };
                }

        
                try
                {
                    var allBansResponse = await client
                        .From<UserBanEntity>()
                        .Get();

                    if (allBansResponse?.Models == null)
                    {
                        _logger.LogWarning("All bans response is null for user {UserId}", userId);
                        return new ApiResponse<bool> { Success = false, Message = "Could not retrieve ban information" };
                    }

                    // Filter in memory 
                    var userBans = allBansResponse.Models.Where(b => 
                        !string.IsNullOrEmpty(b.UserId) && 
                        b.UserId == userId && 
                        b.IsActive &&
                        (!b.ExpiresAt.HasValue || DateTime.UtcNow <= b.ExpiresAt.Value)).ToList();
                    
                    _logger.LogInformation("Found {BanCount} active bans for user {UserId}", userBans.Count, userId);
                    
                    return new ApiResponse<bool> { Success = true, Data = userBans.Any() };
                }
                catch (Exception queryEx)
                {
                    _logger.LogError(queryEx, "Database query failed for user {UserId}: {Message}. Cannot verify ban status", userId, queryEx.Message);
                    
                    return new ApiResponse<bool> { Success = false, Message = "Could not verify ban status" };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user {UserId} is banned", userId);
                return new ApiResponse<bool> { Success = false, Message = "Could not verify ban status" };
            }
        }

        public async Task<ApiResponse<bool>> CheckAndAutoBanUserAsync(string userId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                // Get warning count for user
                var warningsResponse = await client
                    .From<UserWarningEntity>()
                    .Where(x => x.UserId == userId)
                    .Get();

                var warningCount = warningsResponse.Models.Count;

                // If user has 3+ warnings auto ban 
                if (warningCount >= 3)
                {
                    // Check if user is already banned
                    var isBannedResult = await IsUserBannedAsync(userId);
                    if (isBannedResult.Success && isBannedResult.Data)
                    {
                        // User already banned
                        return new ApiResponse<bool> { Success = true, Data = false };
                    }

                    // Create auto ban
                    var autoBanDto = new CreateBanDto
                    {
                        UserId = userId,
                        BanReason = $"Automatic ban due to {warningCount} warnings",
                        BanType = "auto",
                        ExpiresAt = DateTime.UtcNow.AddDays(7) // 7day temporary ban
                    };

                    var banResult = await CreateBanAsync(autoBanDto, "system");
                    return banResult;
                }

                return new ApiResponse<bool> { Success = true, Data = false };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking auto-ban for user {UserId}", userId);
                return new ApiResponse<bool> { Success = false, Message = "Failed to check auto-ban" };
            }
        }

        #endregion

        #region Helper Methods

        private async Task<AdminProfileEntity?> GetAdminProfileAsync(string adminUserId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var adminResponse = await client
                    .From<AdminProfileEntity>()
                    .Where(x => x.UserId == adminUserId)
                    .Get();

                return adminResponse.Models.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting admin profile for user {AdminUserId}", adminUserId);
                return null;
            }
        }

        private async Task UpdateUserProfileStatusAsync(string userId, string status)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                var studentResponse = await client
                    .From<StudentProfileEntity>()
                    .Where(x => x.UserId == userId)
                    .Get();

                if (studentResponse.Models.Any())
                {
                    var student = studentResponse.Models.First();
                    student.Status = status;
                    await client
                        .From<StudentProfileEntity>()
                        .Update(student);
                    _logger.LogInformation("Updated student profile status to {Status} for user {UserId}", status, userId);
                    return;
                }

                var tutorResponse = await client
                    .From<TutorProfileEntity>()
                    .Where(x => x.UserId == userId)
                    .Get();

                if (tutorResponse.Models.Any())
                {
                    var tutor = tutorResponse.Models.First();
                    tutor.Status = status;
                    await client
                        .From<TutorProfileEntity>()
                        .Update(tutor);
                    _logger.LogInformation("Updated tutor profile status to {Status} for user {UserId}", status, userId);
                    return;
                }

                _logger.LogWarning("No profile found for user {UserId} when updating status to {Status}", userId, status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile status for user {UserId} to {Status}", userId, status);
            }
        }

        #endregion
    }
}

