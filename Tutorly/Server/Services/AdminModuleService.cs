using Supabase;
using Tutorly.Server.DatabaseModels;
using Tutorly.Shared;
using static Supabase.Postgrest.Constants;
using System.Collections.Generic;

namespace Tutorly.Server.Services
{
    public class AdminModuleService : IAdminModuleService
    {
        private readonly Supabase.Client _supabaseClient;
        private readonly ILogger<AdminModuleService> _logger;
        private readonly IConfiguration _configuration;

        public AdminModuleService(Supabase.Client supabaseClient, ILogger<AdminModuleService> logger, IConfiguration configuration)
        {
            _supabaseClient = supabaseClient;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<ServiceResult<List<ModuleDto>>> GetAllModulesAsync()
        {
            try
            {
                // Get all modules
                var modulesResponse = await _supabaseClient
                    .From<ModuleEntity>()
                    .Order("module_id", Ordering.Descending)
                    .Get();

                if (modulesResponse?.Models == null)
                {
                    return ServiceResult<List<ModuleDto>>.SuccessResult(new List<ModuleDto>());
                }

                var moduleDtos = new List<ModuleDto>();

                foreach (var module in modulesResponse.Models)
                {
                    var moduleDto = await ConvertToModuleDtoAsync(module);
                    moduleDtos.Add(moduleDto);
                }

                return ServiceResult<List<ModuleDto>>.SuccessResult(moduleDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all modules");
                return ServiceResult<List<ModuleDto>>.FailureResult("Failed to retrieve modules");
            }
        }

        public async Task<ServiceResult<ModuleDto>> GetModuleByIdAsync(int moduleId)
        {
            try
            {
                var moduleResponse = await _supabaseClient
                    .From<ModuleEntity>()
                    .Filter("module_id", Operator.Equals, moduleId)
                    .Single();

                if (moduleResponse == null)
                {
                    return ServiceResult<ModuleDto>.FailureResult("Module not found");
                }

                var moduleDto = await ConvertToModuleDtoAsync(moduleResponse);
                return ServiceResult<ModuleDto>.SuccessResult(moduleDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting module by ID {ModuleId}", moduleId);
                return ServiceResult<ModuleDto>.FailureResult("Failed to retrieve module");
            }
        }

        public async Task<ServiceResult<ModuleDto>> CreateModuleAsync(CreateModuleRequest request)
        {
            try
            {
                _logger.LogInformation("🔍 CreateModuleAsync called with request: {@Request}", request);

                // Validate request
                if (string.IsNullOrWhiteSpace(request.ModuleCode) ||
                    string.IsNullOrWhiteSpace(request.ModuleName) ||
                    string.IsNullOrWhiteSpace(request.ModuleDescription))
                {
                    _logger.LogWarning("❌ Missing required fields - Code: {Code}, Name: {Name}, Description: {Description}",
                        request.ModuleCode, request.ModuleName, request.ModuleDescription);
                    return ServiceResult<ModuleDto>.FailureResult("Module code, name, and description are required");
                }

                _logger.LogInformation("✅ Request validation passed");

                // Check if module code already exists
                _logger.LogInformation("🔍 Checking for existing module with code: {Code}", request.ModuleCode.ToUpper());
                var existingModule = await _supabaseClient
                    .From<ModuleEntity>()
                    .Filter("module_code", Operator.Equals, request.ModuleCode.ToUpper())
                    .Single();

                if (existingModule != null)
                {
                    _logger.LogWarning("❌ Module with code {Code} already exists", request.ModuleCode);
                    return ServiceResult<ModuleDto>.FailureResult("A module with this code already exists");
                }

                _logger.LogInformation("✅ No existing module found with code: {Code}", request.ModuleCode);

                // Use Supabase client with custom JSON serialization to exclude ModuleId
                try
                {
                    _logger.LogInformation("🔍 Using Supabase client with custom JSON serialization");

                    // Create a custom object that only includes the fields we want
                    var insertData = new
                    {
                        module_code = request.ModuleCode.ToUpper(),
                        module_name = request.ModuleName,
                        module_description = request.ModuleDescription
                    };

                    _logger.LogInformation("🔍 Insert data: {@InsertData}", insertData);

                    // Serialize to JSON manually to ensure ModuleId is not included
                    var json = System.Text.Json.JsonSerializer.Serialize(insertData);
                    _logger.LogInformation("🔍 Serialized JSON: {JSON}", json);

                    // Use RPC to insert the module with custom data
                    var result = await _supabaseClient.Rpc("insert_module_json", new Dictionary<string, object>
                    {
                        { "module_data", json }
                    });

                    _logger.LogInformation("🔍 RPC result: {@Result}", result);

                    // If RPC doesn't work, try the simple approach with a new entity
                    if (result == null)
                    {
                        _logger.LogInformation("🔍 RPC failed, trying simple entity approach");

                        // Create a new ModuleEntity but don't set ModuleId
                        var insertEntity = new ModuleEntity
                        {
                            ModuleCode = request.ModuleCode.ToUpper(),
                            ModuleName = request.ModuleName,
                            ModuleDescription = request.ModuleDescription
                        };

                        var insertResult = await _supabaseClient
                            .From<ModuleEntity>()
                            .Insert(insertEntity);

                        _logger.LogInformation("🔍 Simple insert result: {@Result}", new
                        {
                            Success = insertResult?.Models?.Any() == true,
                            ModelCount = insertResult?.Models?.Count() ?? 0,
                            FirstModel = insertResult?.Models?.FirstOrDefault()
                        });

                        if (insertResult?.Models?.FirstOrDefault() == null)
                        {
                            _logger.LogError("❌ Both RPC and simple insert failed");
                            return ServiceResult<ModuleDto>.FailureResult("Failed to create module - no data returned");
                        }

                        var simpleModuleDto = await ConvertToModuleDtoAsync(insertResult.Models.First());
                        _logger.LogInformation("✅ Module created successfully with simple insert: {@ModuleDto}", simpleModuleDto);
                        return ServiceResult<ModuleDto>.SuccessResult(simpleModuleDto);
                    }

                    // Parse the RPC result
                    var createdModule = new ModuleEntity
                    {
                        ModuleId = Convert.ToInt32(result),
                        ModuleCode = request.ModuleCode.ToUpper(),
                        ModuleName = request.ModuleName,
                        ModuleDescription = request.ModuleDescription
                    };

                    _logger.LogInformation("✅ Module created successfully with RPC: {ModuleId}", createdModule.ModuleId);

                    var moduleDto = await ConvertToModuleDtoAsync(createdModule);
                    _logger.LogInformation("✅ Converted to DTO: {@ModuleDto}", moduleDto);

                    return ServiceResult<ModuleDto>.SuccessResult(moduleDto);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Custom JSON serialization approach failed: {Message}", ex.Message);
                    return ServiceResult<ModuleDto>.FailureResult($"Failed to create module: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error creating module: {Message}", ex.Message);
                return ServiceResult<ModuleDto>.FailureResult($"Failed to create module: {ex.Message}");
            }
        }

        public async Task<ServiceResult<ModuleDto>> UpdateModuleAsync(int moduleId, UpdateModuleRequest request)
        {
            try
            {
                _logger.LogInformation("🔍 UpdateModuleAsync called with ID: {ModuleId}, Request: {@Request}", moduleId, request);

                // Validate request
                if (string.IsNullOrWhiteSpace(request.ModuleCode) ||
                    string.IsNullOrWhiteSpace(request.ModuleName) ||
                    string.IsNullOrWhiteSpace(request.ModuleDescription))
                {
                    _logger.LogWarning("❌ Missing required fields - Code: {Code}, Name: {Name}, Description: {Description}",
                        request.ModuleCode, request.ModuleName, request.ModuleDescription);
                    return ServiceResult<ModuleDto>.FailureResult("Module code, name, and description are required");
                }

                _logger.LogInformation("✅ Request validation passed");

                // Check if module exists
                _logger.LogInformation("🔍 Checking if module {ModuleId} exists", moduleId);
                var existingModule = await _supabaseClient
                    .From<ModuleEntity>()
                    .Filter("module_id", Operator.Equals, moduleId)
                    .Single();

                if (existingModule == null)
                {
                    _logger.LogWarning("❌ Module {ModuleId} not found", moduleId);
                    return ServiceResult<ModuleDto>.FailureResult("Module not found");
                }

                _logger.LogInformation("✅ Module {ModuleId} found: {ModuleName}", moduleId, existingModule.ModuleName);

                // Check if another module with the same code exists (excluding current module)
                _logger.LogInformation("🔍 Checking for duplicate module code: {Code}", request.ModuleCode.ToUpper());
                var duplicateModule = await _supabaseClient
                    .From<ModuleEntity>()
                    .Filter("module_code", Operator.Equals, request.ModuleCode.ToUpper())
                    .Filter("module_id", Operator.Not, moduleId)
                    .Single();

                if (duplicateModule != null)
                {
                    _logger.LogWarning("❌ Module with code {Code} already exists (ID: {DuplicateId})",
                        request.ModuleCode, duplicateModule.ModuleId);
                    return ServiceResult<ModuleDto>.FailureResult("A module with this code already exists");
                }

                _logger.LogInformation("✅ No duplicate module code found");

                // Update the module
                _logger.LogInformation("🔍 Updating module {ModuleId} with new data", moduleId);
                var updatedModule = await _supabaseClient
                    .From<ModuleEntity>()
                    .Filter("module_id", Operator.Equals, moduleId)
                    .Set(x => x.ModuleCode, request.ModuleCode.ToUpper())
                    .Set(x => x.ModuleName, request.ModuleName)
                    .Set(x => x.ModuleDescription, request.ModuleDescription)
                    .Update();

                _logger.LogInformation("🔍 Update result: {@Result}", new
                {
                    Success = updatedModule?.Models?.Any() == true,
                    ModelCount = updatedModule?.Models?.Count() ?? 0,
                    FirstModel = updatedModule?.Models?.FirstOrDefault()
                });

                if (updatedModule?.Models?.FirstOrDefault() == null)
                {
                    _logger.LogError("❌ Failed to update module - no models returned");
                    return ServiceResult<ModuleDto>.FailureResult("Failed to update module");
                }

                var moduleDto = await ConvertToModuleDtoAsync(updatedModule.Models.First());
                _logger.LogInformation("✅ Module updated successfully: {@ModuleDto}", moduleDto);
                return ServiceResult<ModuleDto>.SuccessResult(moduleDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error updating module {ModuleId}: {Message}", moduleId, ex.Message);
                return ServiceResult<ModuleDto>.FailureResult($"Failed to update module: {ex.Message}");
            }
        }

        public async Task<ServiceResult> DeleteModuleAsync(int moduleId)
        {
            try
            {
                _logger.LogInformation("🔍 DeleteModuleAsync called with ID: {ModuleId}", moduleId);

                // Check if module exists
                _logger.LogInformation("🔍 Checking if module {ModuleId} exists", moduleId);
                var existingModule = await _supabaseClient
                    .From<ModuleEntity>()
                    .Filter("module_id", Operator.Equals, moduleId)
                    .Single();

                if (existingModule == null)
                {
                    _logger.LogWarning("❌ Module {ModuleId} not found", moduleId);
                    return ServiceResult.FailureResult("Module not found");
                }

                _logger.LogInformation("✅ Module {ModuleId} found: {ModuleName}", moduleId, existingModule.ModuleName);

                // Perform cascading delete
                _logger.LogInformation("🔍 Performing cascading delete for module {ModuleId}", moduleId);
                await PerformCascadingDeleteAsync(moduleId);

                // Delete the module itself
                _logger.LogInformation("🔍 Deleting module {ModuleId}", moduleId);
                await _supabaseClient
                    .From<ModuleEntity>()
                    .Filter("module_id", Operator.Equals, moduleId)
                    .Delete();

                _logger.LogInformation("✅ Module {ModuleId} deleted successfully", moduleId);
                return ServiceResult.SuccessResult("Module deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error deleting module {ModuleId}: {Message}", moduleId, ex.Message);
                return ServiceResult.FailureResult($"Failed to delete module: {ex.Message}");
            }
        }

        private Task PerformCascadingDeleteAsync(int moduleId)
        {
            try
            {
                // For now, we'll only delete the module itself since other tables may not exist yet
                // This can be expanded later when the related tables are created
                _logger.LogInformation("Cascading delete completed for module {ModuleId} (simplified - only module deleted)", moduleId);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cascading delete for module {ModuleId}", moduleId);
                throw; // Re-throw to be handled by the calling method
            }
        }

        private Task<ModuleDto> ConvertToModuleDtoAsync(ModuleEntity module)
        {
            var moduleDto = new ModuleDto
            {
                ModuleId = module.ModuleId,
                ModuleCode = module.ModuleCode,
                ModuleName = module.ModuleName,
                ModuleDescription = module.ModuleDescription,
                UserCount = 0, // Set to 0 for now since related tables may not exist
                ResourceCount = 0,
                TutorCount = 0
            };

            return Task.FromResult(moduleDto);
        }
    }
}
