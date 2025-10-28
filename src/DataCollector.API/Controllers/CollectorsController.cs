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
public class CollectorsController : ControllerBase
{
    private readonly IDataCollectorService _collectorService;
    private readonly ILogger<CollectorsController> _logger;

    public CollectorsController(IDataCollectorService collectorService, ILogger<CollectorsController> logger)
    {
        _collectorService = collectorService;
        _logger = logger;
    }

    /// <summary>
    /// Get all collectors for the current tenant
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CollectorDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = GetTenantId();
            _logger.LogInformation("Retrieving all collectors for tenant: {TenantId}", tenantId);
            
            var collectors = await _collectorService.GetAllAsync(tenantId, cancellationToken);
            return Ok(collectors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving collectors");
            return StatusCode(500, new { message = "An error occurred while retrieving collectors" });
        }
    }

    /// <summary>
    /// Get a specific collector by ID with all pipelines and processing steps
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CollectorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = GetTenantId();
            var collector = await _collectorService.GetByIdAsync(tenantId, id, cancellationToken);
            return Ok(collector);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning("Collector not found: {CollectorId}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving collector: {CollectorId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the collector" });
        }
    }

    /// <summary>
    /// Create a new collector with pipelines and processing steps
    /// </summary>
    /// <remarks>
    /// Sample request:
    /// 
    ///     POST /api/collectors
    ///     {
    ///         "name": "Customer Data Collector",
    ///         "description": "Collects customer data from CRM",
    ///         "pipelines": [
    ///             {
    ///                 "name": "Customer Sync Pipeline",
    ///                 "dataSourceId": "guid-here",
    ///                 "apiPath": "/api/customers",
    ///                 "method": "GET",
    ///                 "processingSteps": [
    ///                     {
    ///                         "name": "Fetch Customers",
    ///                         "type": "api-call",
    ///                         "enabled": true
    ///                     },
    ///                     {
    ///                         "name": "Paginate",
    ///                         "type": "pagination",
    ///                         "enabled": true,
    ///                         "config": { "pageSize": 100 }
    ///                     },
    ///                     {
    ///                         "name": "Transform Data",
    ///                         "type": "transform",
    ///                         "enabled": true
    ///                     },
    ///                     {
    ///                         "name": "Process Each",
    ///                         "type": "for-each",
    ///                         "enabled": true,
    ///                         "childSteps": [
    ///                             {
    ///                                 "name": "Fetch Orders",
    ///                                 "type": "api-call",
    ///                                 "enabled": true
    ///                             }
    ///                         ]
    ///                     },
    ///                     {
    ///                         "name": "Store to DB",
    ///                         "type": "store-database",
    ///                         "enabled": true
    ///                     }
    ///                 ]
    ///             }
    ///         ]
    ///     }
    /// </remarks>
    [HttpPost]
    [Authorize(Roles = "Admin,ProductOwner,Developer")]
    [ProducesResponseType(typeof(CollectorDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateCollectorRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = GetTenantId();
            var userId = GetUserId();
            
            _logger.LogInformation("Creating collector: {Name} for tenant: {TenantId}", request.Name, tenantId);
            
            var collector = await _collectorService.CreateAsync(tenantId, request, userId, cancellationToken);
            
            _logger.LogInformation("Collector created successfully: {CollectorId}", collector.Id);
            return CreatedAtAction(nameof(GetById), new { id = collector.Id }, collector);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning("Failed to create collector: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning("Resource not found while creating collector: {Message}", ex.Message);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating collector");
            return StatusCode(500, new { message = "An error occurred while creating the collector" });
        }
    }

    /// <summary>
    /// Update an existing collector (only in Draft or Dev stage)
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,ProductOwner,Developer")]
    [ProducesResponseType(typeof(CollectorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateCollectorRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = GetTenantId();
            var userId = GetUserId();
            
            _logger.LogInformation("Updating collector: {CollectorId}", id);
            
            var collector = await _collectorService.UpdateAsync(tenantId, id, request, userId, cancellationToken);
            
            _logger.LogInformation("Collector updated successfully: {CollectorId}", id);
            return Ok(collector);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning("Collector not found: {CollectorId}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (DomainException ex)
        {
            _logger.LogWarning("Failed to update collector {CollectorId}: {Message}", id, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating collector: {CollectorId}", id);
            return StatusCode(500, new { message = "An error occurred while updating the collector" });
        }
    }

    /// <summary>
    /// Delete a collector (soft delete - only in Draft or Dev stage)
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,ProductOwner")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = GetTenantId();
            var userId = GetUserId();
            
            _logger.LogInformation("Deleting collector: {CollectorId}", id);
            
            var success = await _collectorService.DeleteAsync(tenantId, id, userId, cancellationToken);
            
            if (success)
            {
                _logger.LogInformation("Collector deleted successfully: {CollectorId}", id);
                return NoContent();
            }
            
            return NotFound(new { message = "Collector not found" });
        }
        catch (DomainException ex)
        {
            _logger.LogWarning("Failed to delete collector {CollectorId}: {Message}", id, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting collector: {CollectorId}", id);
            return StatusCode(500, new { message = "An error occurred while deleting the collector" });
        }
    }

    /// <summary>
    /// Execute a specific pipeline in a collector
    /// </summary>
    [HttpPost("{id:guid}/execute")]
    [Authorize(Roles = "Admin,ProductOwner,Developer,Collector")]
    [ProducesResponseType(typeof(CollectorExecutionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Execute(Guid id, [FromBody] ExecuteCollectorRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = GetTenantId();
            
            _logger.LogInformation("Executing collector: {CollectorId}, Pipeline: {PipelineId}", id, request.PipelineId);
            
            var result = await _collectorService.ExecuteAsync(tenantId, id, request.PipelineId, cancellationToken);
            
            _logger.LogInformation("Collector execution completed: {CollectorId}, Success: {Success}", id, result.Success);
            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning("Resource not found during execution: {Message}", ex.Message);
            return NotFound(new { message = ex.Message });
        }
        catch (DomainException ex)
        {
            _logger.LogWarning("Failed to execute collector {CollectorId}: {Message}", id, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing collector: {CollectorId}", id);
            return StatusCode(500, new { message = "An error occurred while executing the collector" });
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

    private string GetUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Unknown";
    }
}
