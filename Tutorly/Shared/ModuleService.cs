using System.Net.Http.Json;
using Tutorly.Shared;

namespace Tutorly.Shared
{
    public class ModuleService
    {
        private readonly HttpClient _httpClient;

        public ModuleService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<Module>> GetAllModules()
        {
            try
            {
                var modules = await _httpClient.GetFromJsonAsync<List<Module>>("api/module");
                return modules ?? new List<Module>();
            }
            catch
            {
                Console.WriteLine("could not load modules");
                return new List<Module>();
            }
        }


        public async Task<Module?> GetModuleByName(string name)
        {
            try
            {
                var module = await _httpClient.GetFromJsonAsync<Module>($"api/module/byname?name={name}");
                return module;
            }
            catch
            {
                Console.WriteLine("could not load modules");
                return null;
            }
        }

        public async Task<Module?> GetModuleById(int id)
        {
            try
            {
                var module = await _httpClient.GetFromJsonAsync<Module>($"api/module/{id}");
                return module;
            }
            catch
            {
                Console.WriteLine("could not load modules");
                return null;
            }
        }

        public async Task<List<TutorSummary>> GetTutorsByModuleId(int moduleId)
        {
            try
            {
                var tutors = await _httpClient.GetFromJsonAsync<List<TutorSummary>>($"api/module/{moduleId}/tutors");
                return tutors ?? new List<TutorSummary>();
            }
            catch
            {
                Console.WriteLine("could not load tutors");
                return new List<TutorSummary>();
            }
        }

        public async Task<List<StudentModuleTutor>> GetStudentModulesWithTutors(int studentId)
        {
            try
            {
                var modules = await _httpClient.GetFromJsonAsync<List<StudentModuleTutor>>($"api/student/{studentId}/modules-with-tutors");
                return modules ?? new List<StudentModuleTutor>();
            }
            catch
            {
                //return StatusCode(500, new { message = "f"});
                Console.WriteLine("could not load tutor modules");
                return new List<StudentModuleTutor>();
            }
        }
    }
}
