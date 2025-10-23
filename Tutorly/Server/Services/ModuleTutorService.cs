using Tutorly.Server.Helpers;
using Tutorly.Server.DatabaseModels;
using Tutorly.Shared;

namespace Tutorly.Server.Services
{
    public interface IModuleTutorService
    {
        Task<ApiResponse<bool>> AssignTutorToModuleAsync(int tutorId, int moduleId);
        Task<ApiResponse<bool>> UnassignTutorFromModuleAsync(int tutorId, int moduleId);
    }

    public class ModuleTutorService : IModuleTutorService
    {
        private readonly ISupabaseClientFactory _supabaseFactory;
        private readonly IForumService _forumService;
        private readonly ILogger<ModuleTutorService> _logger;

        public ModuleTutorService(
            ISupabaseClientFactory supabaseFactory,
            IForumService forumService,
            ILogger<ModuleTutorService> logger)
        {
            _supabaseFactory = supabaseFactory;
            _forumService = forumService;
            _logger = logger;
        }

        public async Task<ApiResponse<bool>> AssignTutorToModuleAsync(int tutorId, int moduleId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                // Check if the assignment already exists
                var existingAssignment = await client
                    .From<ModuleTutorEntity>()
                    .Where(x => x.TutorId == tutorId && x.ModuleId == moduleId)
                    .Get();

                if (existingAssignment.Models.Any())
                {
                    _logger.LogInformation($"Tutor {tutorId} is already assigned to module {moduleId}");
                    return new ApiResponse<bool> { Success = true, Data = true, Message = "Already assigned" };
                }

                // Create the module-tutor assignment
                var assignment = new ModuleTutorEntity
                {
                    TutorId = tutorId,
                    ModuleId = moduleId
                };

                await client
                    .From<ModuleTutorEntity>()
                    .Insert(assignment);

                _logger.LogInformation($"Successfully assigned tutor {tutorId} to module {moduleId}");

                // Automatically follow the module community
                var followResult = await _forumService.AutoFollowModuleCommunityAsync(tutorId, moduleId);
                
                if (!followResult.Success)
                {
                    _logger.LogWarning($"Failed to auto-follow module community for tutor {tutorId} and module {moduleId}: {followResult.Message}");
                    // Don't fail the assignment if following fails
                }

                return new ApiResponse<bool> { Success = true, Data = true, Message = "Successfully assigned tutor to module" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning tutor to module");
                return new ApiResponse<bool> { Success = false, Message = "Failed to assign tutor to module" };
            }
        }

        public async Task<ApiResponse<bool>> UnassignTutorFromModuleAsync(int tutorId, int moduleId)
        {
            try
            {
                var client = _supabaseFactory.CreateService();

                // Remove the module-tutor assignment
                await client
                    .From<ModuleTutorEntity>()
                    .Where(x => x.TutorId == tutorId && x.ModuleId == moduleId)
                    .Delete();

                _logger.LogInformation($"Successfully unassigned tutor {tutorId} from module {moduleId}");

                // Automatically unfollow the module community
                var unfollowResult = await _forumService.AutoUnfollowModuleCommunityAsync(tutorId, moduleId);
                
                if (!unfollowResult.Success)
                {
                    _logger.LogWarning($"Failed to auto-unfollow module community for tutor {tutorId} and module {moduleId}: {unfollowResult.Message}");
                    // Don't fail the unassignment if unfollowing fails
                }

                return new ApiResponse<bool> { Success = true, Data = true, Message = "Successfully unassigned tutor from module" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unassigning tutor from module");
                return new ApiResponse<bool> { Success = false, Message = "Failed to unassign tutor from module" };
            }
        }
    }
}
