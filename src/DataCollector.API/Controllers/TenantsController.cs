using Microsoft.AspNetCore.Mvc;
namespace DataCollector.API.Controllers;
[ApiController]
[Route("api/[controller]")]
public class TenantsController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateTenant([FromBody] object request)
    {
        return Ok(new { message = "Tenant creation endpoint" });
    }
}
