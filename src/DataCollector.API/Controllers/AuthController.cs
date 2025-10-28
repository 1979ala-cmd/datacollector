using Microsoft.AspNetCore.Mvc;
namespace DataCollector.API.Controllers;
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] object request)
    {
        return Ok(new { message = "Login endpoint" });
    }
    
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] object request)
    {
        return Ok(new { message = "Register endpoint" });
    }
}
