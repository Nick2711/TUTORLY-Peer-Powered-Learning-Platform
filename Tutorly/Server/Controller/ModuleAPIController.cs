using Microsoft.AspNetCore.Mvc;
using Tutorly.Shared;
using Tutorly.Server.Helpers;
using Tutorly.Server.Handler;
using Tutorly.Server.DatabaseModels;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Tutorly.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ModuleAPIController : ControllerBase
    {
        private readonly SupabaseClientFactory _client;
        private readonly ModuleHandler _handler;

        public ModuleAPIController(SupabaseClientFactory clientFactory, ModuleHandler handler)
        {
            _client = clientFactory;
            _handler = handler;
        }

        // GET: api/modules
        [HttpGet]
        public async Task<ActionResult<List<Module>>> GetModules()
        {
            var entities = await _client.GetEntities<ModuleEntity>();
            var modules = entities.Select(m => new Module
            {
                ModuleId = m.ModuleId,
                ModuleCode = m.ModuleCode,
                ModuleName = m.ModuleName,
                ModuleDescription = m.ModuleDescription
            }).ToList();

            return Ok(modules);
        }

        // GET: api/moduleapi/byname?name=XYZ
        [HttpGet("byname")]
        public async Task<ActionResult<Module>> GetModuleByName([FromQuery] string name)
        {
            if (!_handler.ValidateModuleName(name, out string message))
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

        // GET: api/modules/5
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Module>> GetModuleById(int id)
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

        // POST: api/modules
        [HttpPost]
        public async Task<ActionResult> CreateModule([FromBody] Module module)
        {
            if (!_handler.ValidateModule(module, out string validationMessage))
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
    }
}
