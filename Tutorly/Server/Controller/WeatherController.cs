using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Route("api/[controller]")]
[ApiController]
public class WeatherController : ControllerBase
{
    [HttpGet("public")]
    public IActionResult Public() => Ok(new { msg = "Anyone can see this." });

    [HttpGet("private")]
    [Authorize]
    public IActionResult Private() => Ok(new { msg = "Only logged-in users see this." });

    [HttpGet("admin")]
    [Authorize(Policy = "AdminOnly")]
    public IActionResult Admin() => Ok(new { msg = "Admins only." });
}
