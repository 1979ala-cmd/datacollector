using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
namespace DataCollector.API.Controllers;
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DataSourcesController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(new { message = "Get all data sources" });
    }
    
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] object request)
    {
        return Ok(new { message = "Create data source" });
    }
}
