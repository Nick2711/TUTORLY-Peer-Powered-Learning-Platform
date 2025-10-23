using Microsoft.AspNetCore.Mvc;
using Tutorly.Server.DatabaseModels;
using Tutorly.Server.Handler;
using Tutorly.Shared;



[ApiController]
public class TopicSubscriptionApiController : ControllerBase
{
    private readonly TopicSubscriptionHandler _handler;

    public TopicSubscriptionApiController(TopicSubscriptionHandler handler)
    {
        _handler = handler;
    }

    [HttpPost]
    [Route("api/subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] TopicSubscriptionEntity subscription)
    {
        bool success = await _handler.SubscribeStudent(subscription);

        if (!success)
            return BadRequest("Invalid subscription request.");

        return Ok("Subscribed successfully.");
    }

    // Subscribe a student to all topics under a module
    [HttpPost]
    [Route("api/subscribe/module")]
    public async Task<IActionResult> SubscribeByModule([FromBody] SubscribeByModuleRequest req)
    {
        if (req == null || req.StudentId <= 0 || req.ModuleId <= 0)
            return BadRequest("Invalid request body.");

        var ok = await _handler.SubscribeStudentToModuleTopics(req.StudentId, req.ModuleId);
        if (!ok) return BadRequest("Subscription failed.");
        return Ok("Subscribed to module topics.");
    }

    public class SubscribeByModuleRequest
    {
        public int StudentId { get; set; }
        public int ModuleId { get; set; }
    }

}
