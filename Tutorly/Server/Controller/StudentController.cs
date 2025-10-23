using Microsoft.AspNetCore.Mvc;
using Supabase;
using Supabase.Functions;
using Tutorly.Server.DatabaseModels;
using Tutorly.Server.Helpers;
using Tutorly.Shared;
using SupaClient = Supabase.Client;
using System.Text.Json;

namespace Tutorly.Server.Controller
{
    [ApiController]
    [Route("api/student")] 
    public class StudentController : ControllerBase
    {
        private readonly SupaClient _client;

        public StudentController(SupaClient client)
        {
            _client = client;
        }

        // GET api/student/{studentId}/modules
        [HttpGet("{studentId}/modules")]
        public async Task<IActionResult> GetStudentModules(int studentId)
        {
            try
            {
                var rows = await _client.From<ModuleStudentEntity>()
                    .Filter("student_id", Supabase.Postgrest.Constants.Operator.Equals, studentId)
                    .Get();
                return Ok(rows.Models);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving student modules.", error = ex.Message });
            }
        }

        // GET api/student/{studentId}/subscriptions
        [HttpGet("{studentId}/subscriptions")]
        public async Task<IActionResult> GetStudentSubscriptions(int studentId)
        {
            try
            {
                var rows = await _client.From<TopicSubscriptionEntity>()
                    .Filter("student_id", Supabase.Postgrest.Constants.Operator.Equals, studentId)
                    .Get();
                return Ok(rows.Models);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving student subscriptions.", error = ex.Message });
            }
        }

        // GET api/student/{studentId}/modules-with-tutors
        [HttpGet("{studentId}/modules-with-tutors")]
        public async Task<IActionResult> GetStudentModulesWithTutors(int studentId)
        {
            try
            {
                // Get student's enrolled modules
                var studentModules = await _client.From<ModuleStudentEntity>()
                    .Filter("student_id", Supabase.Postgrest.Constants.Operator.Equals, studentId)
                    .Get();

                var result = new List<StudentModuleTutor>();

                foreach (var studentModule in studentModules.Models)
                {
                    // Get tutors for this module
                    var moduleTutors = await _client.From<ModuleTutorEntity>()
                        .Filter("module_id", Supabase.Postgrest.Constants.Operator.Equals, studentModule.ModuleId)
                        .Get();

                    // Get module information
                    var module = await _client.From<ModuleEntity>()
                        .Filter("module_id", Supabase.Postgrest.Constants.Operator.Equals, studentModule.ModuleId)
                        .Get();

                    if (module.Models.Any() == false) continue; //dont use !

                    var moduleData = module.Models.First();

                    foreach (var moduleTutor in moduleTutors.Models)
                    {
                        var tutorProfile = await _client.From<TutorProfileEntity>()
                            .Filter("tutor_id", Supabase.Postgrest.Constants.Operator.Equals, moduleTutor.TutorId)
                            .Get();

                        var tutorData = tutorProfile.Models.FirstOrDefault();

                        result.Add(new StudentModuleTutor
                        {
                            TutorId = moduleTutor.TutorId,
                            TutorName = tutorData?.FullName ?? $"Tutor {moduleTutor.TutorId}",
                            ModuleCode = moduleData.ModuleCode,
                            ModuleName = moduleData.ModuleName,
                            TutorPhoto = tutorData?.AvatarUrl ?? "https://i.pravatar.cc/40?img=3", //change to profile image
                            Rating = tutorData?.Rating ?? 0.0,
                            Stars = GenerateStars(tutorData?.Rating ?? 0.0)
                        });
                    }
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving student modules with tutors.", error = ex.Message });
            }
        }


        private string GenerateStars(double rating)
        {
            var fullStars = (int)Math.Floor(rating);
            var hasHalfStar = rating - fullStars >= 0.5;
            var emptyStars = 5 - fullStars - (hasHalfStar ? 1 : 0);

            var stars = new string('★', fullStars);
            if (hasHalfStar) stars += /*"☆"*/" ✭";
            stars += new string('☆', emptyStars);

            return stars;
        }
    }
}


