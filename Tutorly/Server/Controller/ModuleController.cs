using Microsoft.AspNetCore.Mvc;
using Tutorly.Shared;
using Tutorly.Server.Handler;
using Tutorly.Server.DatabaseModels;
using Tutorly.Server.Helpers;
using Tutorly.Server.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Tutorly.Server.Controller
{

    [ApiController]
    [Route("api/[controller]")]
    public class ModuleController : ControllerBase
    {
        private readonly ModuleHandler _moduleHandler;
        private readonly SupabaseClientFactory _client;

        public ModuleController(ModuleHandler moduleHandler, SupabaseClientFactory clientFactory)
        {
            _moduleHandler = moduleHandler;
            _client = clientFactory;
        }

        [HttpGet]
        public async Task<IActionResult> GetModules()
        {
            try
            {
                var modules = await _moduleHandler.GetAllModulesAsync();
                return Ok(modules);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving modules.", error = ex.Message });
            }
        }

        // GET: api/module/byname?name=XYZ
        [HttpGet("byname")]
        public async Task<ActionResult<Module>> GetModuleByName([FromQuery] string name)
        {
            try
            {
                if (!_moduleHandler.ValidateModuleName(name, out string message))
                    return BadRequest(message);

                var entities = await _client.GetEntities<ModuleEntity>();
                var entity = entities.FirstOrDefault(m => m.ModuleName.Equals(name, System.StringComparison.OrdinalIgnoreCase));

                if (entity == null)
                    return NotFound();

                var module = new Module
                {
                    ModuleId = entity.ModuleId,
                    ModuleCode = entity.ModuleCode,
                    ModuleName = entity.ModuleName,
                    ModuleDescription = entity.ModuleDescription
                };

                return Ok(module);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving the module by name.", error = ex.Message });
            }
        }

        // GET: api/module/5
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Module>> GetModuleById(int id)
        {
            try
            {
                var entities = await _client.GetEntities<ModuleEntity>();
                var entity = entities.FirstOrDefault(e => e.ModuleId == id);

                if (entity == null)
                    return NotFound();

                var module = new Module
                {
                    ModuleId = entity.ModuleId,
                    ModuleCode = entity.ModuleCode,
                    ModuleName = entity.ModuleName,
                    ModuleDescription = entity.ModuleDescription
                };

                return Ok(module);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving the module by ID.", error = ex.Message });
            }
        }

        // POST: api/module
        [HttpPost]
        public async Task<ActionResult> CreateModule([FromBody] Module module)
        {
            try
            {
                if (!_moduleHandler.ValidateModule(module, out string validationMessage))
                    return BadRequest(validationMessage);

                var entity = new ModuleEntity
                {
                    ModuleCode = module.ModuleCode,
                    ModuleName = module.ModuleName,
                    ModuleDescription = module.ModuleDescription
                };

                await _client.AddEntity(entity);

                return Ok(new { message = "Module created successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while creating the module.", error = ex.Message });
            }
        }

        // GET api/module/{moduleId}/topics
        [HttpGet("{moduleId}/topics")]
        public async Task<IActionResult> GetModuleTopics([FromServices] Supabase.Client client, int moduleId)
        {
            try
            {
                var resp = await client.From<TopicEntity>()
                    .Filter("module_id", Supabase.Postgrest.Constants.Operator.Equals, moduleId)
                    .Get();
                return Ok(resp.Models);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving module topics.", error = ex.Message });
            }
        }

        // GET api/module/{moduleId}/tutors
        [HttpGet("{moduleId}/tutors")]
        public async Task<IActionResult> GetModuleTutors([FromServices] Supabase.Client client, int moduleId)
        {
            try
            {
                var map = await client.From<ModuleTutorEntity>()
                    .Filter("module_id", Supabase.Postgrest.Constants.Operator.Equals, moduleId)
                    .Get();

                var tutorIds = map.Models.Select(m => m.TutorId).Distinct().ToList();
                if (tutorIds.Count == 0) return Ok(Array.Empty<object>());

                var tutorsResp = await client.From<TutorProfileEntity>()
                    .Filter("tutor_id", Supabase.Postgrest.Constants.Operator.In, tutorIds.Cast<object>().ToArray())
                    .Get();

                var result = tutorsResp.Models
                    .Select(t => new TutorSummary
                    {
                        Tutor_Id = t.TutorId,
                        UserId = t.UserId,
                        Full_Name = t.FullName,
                        Role = t.Role,
                        Photo = !string.IsNullOrEmpty(t.AvatarUrl) ? t.AvatarUrl : $"https://i.pravatar.cc/80?img={t.TutorId % 20}",
                        Blurb = t.Blurb,
                        Rating = t.Rating,
                        Stars = GenerateStars(t.Rating)
                    })
                    .ToList();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving module tutors.", error = ex.Message });
            }
        }

        // GET api/module/tutor/{tutorId}
        [HttpGet("tutor/{tutorId}")]
        public async Task<IActionResult> GetTutorModules([FromServices] Supabase.Client client, int tutorId)
        {
            try
            {
                // Get modules assigned to this tutor
                var moduleTutors = await client.From<ModuleTutorEntity>()
                    .Filter("tutor_id", Supabase.Postgrest.Constants.Operator.Equals, tutorId)
                    .Get();

                if (!moduleTutors.Models.Any())
                {
                    return Ok(new List<Module>());
                }

                var moduleIds = moduleTutors.Models.Select(mt => mt.ModuleId).ToList();

                // Get module details
                var modules = await client.From<ModuleEntity>()
                    .Filter("module_id", Supabase.Postgrest.Constants.Operator.In, moduleIds.Cast<object>().ToArray())
                    .Get();

                var result = modules.Models.Select(m => new Module
                {
                    ModuleId = m.ModuleId,
                    ModuleCode = m.ModuleCode,
                    ModuleName = m.ModuleName,
                    ModuleDescription = m.ModuleDescription
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving tutor modules.", error = ex.Message });
            }
        }

        // POST: api/module/assign-tutor
        [HttpPost("assign-tutor")]
        public async Task<IActionResult> AssignTutorToModule([FromBody] AssignTutorToModuleRequest request, [FromServices] IModuleTutorService moduleTutorService)
        {
            try
            {
                var result = await moduleTutorService.AssignTutorToModuleAsync(request.TutorId, request.ModuleId);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while assigning tutor to module.", error = ex.Message });
            }
        }

        // POST: api/module/unassign-tutor
        [HttpPost("unassign-tutor")]
        public async Task<IActionResult> UnassignTutorFromModule([FromBody] AssignTutorToModuleRequest request, [FromServices] IModuleTutorService moduleTutorService)
        {
            try
            {
                var result = await moduleTutorService.UnassignTutorFromModuleAsync(request.TutorId, request.ModuleId);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while unassigning tutor from module.", error = ex.Message });
            }
        }

        private string GenerateStars(double rating)
        {
            int fullStars = (int)Math.Floor(rating);
            bool hasHalfStar = rating - fullStars >= 0.5;

            string stars = new string('★', fullStars);
            if (hasHalfStar && fullStars < 5)
            {
                stars += "☆";
            }
            stars += new string('☆', 5 - fullStars - (hasHalfStar ? 1 : 0));

            return stars;
        }
    }

    public class AssignTutorToModuleRequest
    {
        public int TutorId { get; set; }
        public int ModuleId { get; set; }
    }
}



