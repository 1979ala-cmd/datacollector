using System.Security.Claims;
using DataCollector.Application.DTOs;
using DataCollector.Application.Interfaces;
using DataCollector.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataCollector.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DataSourcesController : ControllerBase
{
    private readonly IDataSourceService _dataSourceService;
    private readonly ILogger<DataSourcesController> _logger;

    public DataSourcesController(IDataSourceService dataSourceService, ILogger<DataSourcesController> logger)
    {
        _dataSourceService = dataSourceService;
        _logger = logger;
    }

    /// <summary>
    /// Get all data sources for the current tenant
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<DataSourceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = GetTenantId();
            _logger.LogInformation("Retrieving all data sources for tenant: {TenantId}", tenantId);
            
            var dataSources = await _dataSourceService.GetAllAsync(tenantId, cancellationToken);
            return Ok(dataSources);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving data sources");
            return StatusCode(500, new { message = "An error occurred while retrieving data sources" });
        }
    }

    /// <summary>
    /// Get a specific data source by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DataSourceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = GetTenantId();
            var dataSource = await _dataSourceService.GetByIdAsync(tenantId, id, cancellationToken);
            return Ok(dataSource);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning("Data source not found: {DataSourceId}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving data source: {DataSourceId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the data source" });
        }
    }

    /// <summary>
    /// Create a new data source
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,ProductOwner,Developer")]
    [ProducesResponseType(typeof(DataSourceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateDataSourceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = GetTenantId();
            _logger.LogInformation("Creating data source: {Name} for tenant: {TenantId}", request.Name, tenantId);
            
            var dataSource = await _dataSourceService.CreateAsync(tenantId, request, cancellationToken);
            
            _logger.LogInformation("Data source created successfully: {DataSourceId}", dataSource.Id);
            return CreatedAtAction(nameof(GetById), new { id = dataSource.Id }, dataSource);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning("Failed to create data source: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating data source");
            return StatusCode(500, new { message = "An error occurred while creating the data source" });
        }
    }

    /// <summary>
    /// Update an existing data source
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,ProductOwner,Developer")]
    [ProducesResponseType(typeof(DataSourceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateDataSourceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = GetTenantId();
            _logger.LogInformation("Updating data source: {DataSourceId}", id);
            
            var dataSource = await _dataSourceService.UpdateAsync(tenantId, id, request, cancellationToken);
            
            _logger.LogInformation("Data source updated successfully: {DataSourceId}", id);
            return Ok(dataSource);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning("Data source not found: {DataSourceId}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (DomainException ex)
        {
            _logger.LogWarning("Failed to update data source {DataSourceId}: {Message}", id, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating data source: {DataSourceId}", id);
            return StatusCode(500, new { message = "An error occurred while updating the data source" });
        }
    }

    /// <summary>
    /// Delete a data source (soft delete)
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,ProductOwner")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = GetTenantId();
            _logger.LogInformation("Deleting data source: {DataSourceId}", id);
            
            var success = await _dataSourceService.DeleteAsync(tenantId, id, cancellationToken);
            
            if (success)
            {
                _logger.LogInformation("Data source deleted successfully: {DataSourceId}", id);
                return NoContent();
            }
            
            return NotFound(new { message = "Data source not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting data source: {DataSourceId}", id);
            return StatusCode(500, new { message = "An error occurred while deleting the data source" });
        }
    }

    /// <summary>
    /// Test connection to a data source
    /// </summary>
    [HttpPost("{id:guid}/test")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TestConnection(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = GetTenantId();
            _logger.LogInformation("Testing connection for data source: {DataSourceId}", id);
            
            var success = await _dataSourceService.TestConnectionAsync(tenantId, id, cancellationToken);
            
            return Ok(new 
            { 
                success, 
                message = success ? "Connection test successful" : "Connection test failed",
                testedAt = DateTime.UtcNow
            });
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning("Data source not found: {DataSourceId}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing connection for data source: {DataSourceId}", id);
            return StatusCode(500, new { message = "An error occurred while testing the connection" });
        }
    }

    private Guid GetTenantId()
    {
        var tenantIdClaim = User.FindFirst("tenantId")?.Value;
        if (string.IsNullOrEmpty(tenantIdClaim) || !Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            throw new DomainException("Tenant ID not found in token");
        }
        return tenantId;
    }
}
