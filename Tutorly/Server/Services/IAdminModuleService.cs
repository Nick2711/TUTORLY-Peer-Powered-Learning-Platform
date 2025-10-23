using Tutorly.Shared;

namespace Tutorly.Server.Services
{
    public interface IAdminModuleService
    {
        Task<ServiceResult<List<ModuleDto>>> GetAllModulesAsync();
        Task<ServiceResult<ModuleDto>> GetModuleByIdAsync(int moduleId);
        Task<ServiceResult<ModuleDto>> CreateModuleAsync(CreateModuleRequest request);
        Task<ServiceResult<ModuleDto>> UpdateModuleAsync(int moduleId, UpdateModuleRequest request);
        Task<ServiceResult> DeleteModuleAsync(int moduleId);
    }
}
