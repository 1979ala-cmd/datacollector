using DataCollector.Application.DTOs;
using DataCollector.Application.Interfaces;
using DataCollector.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataCollector.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TenantsController : ControllerBase
{
    private readonly ITenantService _tenantService;
    private readonly ILogger<TenantsController> _logger;

    public TenantsController(ITenantService tenantService, ILogger<TenantsController> logger)
    {
        _tenantService = tenantService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new tenant with admin user
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TenantCreatedResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Creating tenant: {TenantName}", request.Name);
            
            var result = await _tenantService.CreateTenantAsync(request, cancellationToken);
            
            _logger.LogInformation("Tenant created successfully: {TenantId}", result.TenantId);
            return CreatedAtAction(nameof(GetTenant), new { id = result.TenantId }, result);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning("Failed to create tenant {TenantName}: {Message}", request.Name, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tenant: {TenantName}", request.Name);
            return StatusCode(500, new { message = "An error occurred while creating the tenant" });
        }
    }

    /// <summary>
    /// Get tenant by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTenant(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var tenant = await _tenantService.GetTenantAsync(id, cancellationToken);
            return Ok(tenant);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning("Tenant not found: {TenantId}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tenant: {TenantId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the tenant" });
        }
    }

    /// <summary>
    /// Get tenant by slug
    /// </summary>
    [HttpGet("by-slug/{slug}")]
    [Authorize]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTenantBySlug(string slug, CancellationToken cancellationToken)
    {
        try
        {
            var tenant = await _tenantService.GetTenantBySlugAsync(slug, cancellationToken);
            return Ok(tenant);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning("Tenant not found with slug: {Slug}", slug);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tenant by slug: {Slug}", slug);
            return StatusCode(500, new { message = "An error occurred while retrieving the tenant" });
        }
    }

    /// <summary>
    /// Get all tenants
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IEnumerable<TenantDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllTenants(CancellationToken cancellationToken)
    {
        try
        {
            var tenants = await _tenantService.GetAllTenantsAsync(cancellationToken);
            return Ok(tenants);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all tenants");
            return StatusCode(500, new { message = "An error occurred while retrieving tenants" });
        }
    }

    /// <summary>
    /// Provision database for a tenant (manual trigger if auto-creation is disabled)
    /// </summary>
    [HttpPost("{id:guid}/provision")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ProvisionTenantDatabase(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Provisioning database for tenant: {TenantId}", id);
            
            var success = await _tenantService.ProvisionTenantDatabaseAsync(id, cancellationToken);
            
            if (success)
            {
                _logger.LogInformation("Database provisioned successfully for tenant: {TenantId}", id);
                return Ok(new { message = "Database provisioned successfully" });
            }
            
            return BadRequest(new { message = "Failed to provision database" });
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning("Tenant not found: {TenantId}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (DomainException ex)
        {
            _logger.LogWarning("Failed to provision database for tenant {TenantId}: {Message}", id, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error provisioning database for tenant: {TenantId}", id);
            return StatusCode(500, new { message = "An error occurred while provisioning the database" });
        }
    }
}
