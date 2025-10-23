using Supabase;
using Supabase.Postgrest;
using Tutorly.Shared;
using Tutorly.Server.DatabaseModels;

namespace Tutorly.Server.Handler
{

    public class ModuleHandler
    {
        private readonly Supabase.Client _client;

        public ModuleHandler(Supabase.Client client)
        {
            _client = client;


        }
        public async Task<List<Module>> GetAllModulesAsync()
        {
            // Fetch from Supabase table "modules" and map to shared DTO
            var response = await _client
                .From<DatabaseModels.ModuleEntity>()
                .Get();

            var results = new List<Module>();
            foreach (var row in response.Models)
            {
                results.Add(new Module
                {
                    ModuleId = row.ModuleId,
                    ModuleCode = row.ModuleCode,
                    ModuleName = row.ModuleName,
                    ModuleDescription = row.ModuleDescription,
                    ModuleDepartment = string.Empty // not stored in DB model yet
                });
            }
            return results;
        }
        public bool ValidateModule(Module module, out string validationMessage)
        {
            if (module == null)
            {
                validationMessage = "Module cannot be null.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(module.ModuleCode))
            {
                validationMessage = "Module code is required.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(module.ModuleName))
            {
                validationMessage = "Module name is required.";
                return false;
            }

            validationMessage = string.Empty;
            return true;
        }

        public bool ValidateModuleName(string name, out string message)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                message = "Module name cannot be empty.";
                return false;
            }
            message = string.Empty;
            return true;
        }

    }
}




