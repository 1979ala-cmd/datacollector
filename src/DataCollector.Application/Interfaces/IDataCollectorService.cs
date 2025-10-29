
using DataCollector.Domain.DTOs;
using DataCollector.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace DataCollector.Application.Interfaces;

/// <summary>
/// Service for managing Data Collectors with function-based pipelines
/// Each collector uses ONE DataSource but can have MULTIPLE pipelines
/// Each pipeline references a function from the DataSource
/// </summary>
public interface IDataCollectorService
{
    // ============================================================================
    // CRUD Operations
    // ============================================================================
    
    /// <summary>
    /// Create a new collector with pipelines
    /// All pipelines must reference functions from the same DataSource
    /// Validates that all function IDs exist in the specified DataSource
    /// </summary>
    Task<CollectorDetailDto> CreateAsync(
        Guid tenantId, 
        CreateCollectorRequest request, 
        string createdBy, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get collector by ID with full details including DataSource info
    /// </summary>
    Task<CollectorDetailDto> GetByIdAsync(
        Guid tenantId, 
        Guid id, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all collectors for a tenant (summary view)
    /// </summary>
    Task<IEnumerable<CollectorDto>> GetAllAsync(
        Guid tenantId, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update collector (only allowed in Draft/Dev stages)
    /// </summary>
    Task<CollectorDetailDto> UpdateAsync(
        Guid tenantId, 
        Guid id, 
        CreateCollectorRequest request, 
        string updatedBy, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete collector (soft delete, only allowed in Draft/Dev stages)
    /// </summary>
    Task<bool> DeleteAsync(
        Guid tenantId, 
        Guid id, 
        string deletedBy, 
        CancellationToken cancellationToken = default);
    
    // ============================================================================
    // Pipeline Management
    // ============================================================================
    
    /// <summary>
    /// Add a new pipeline to an existing collector
    /// Pipeline must use a function from the collector's DataSource
    /// </summary>
    Task<PipelineDetailDto> AddPipelineAsync(
        Guid tenantId,
        Guid collectorId,
        CreatePipelineRequest request,
        string createdBy,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update an existing pipeline
    /// </summary>
    Task<PipelineDetailDto> UpdatePipelineAsync(
        Guid tenantId,
        Guid collectorId,
        Guid pipelineId,
        CreatePipelineRequest request,
        string updatedBy,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Remove a pipeline from a collector
    /// </summary>
    Task<bool> RemovePipelineAsync(
        Guid tenantId,
        Guid collectorId,
        Guid pipelineId,
        string deletedBy,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get pipeline details with function information
    /// </summary>
    Task<PipelineDetailDto> GetPipelineAsync(
        Guid tenantId,
        Guid collectorId,
        Guid pipelineId,
        CancellationToken cancellationToken = default);
    
    // ============================================================================
    // Execution
    // ============================================================================
    
    /// <summary>
    /// Execute a specific pipeline in a collector
    /// Resolves function from DataSource, applies parameter mappings, and executes
    /// </summary>
    Task<Doamin.DTOs.CollectorExecutionResult> ExecuteAsync(
        Guid tenantId, 
        Guid collectorId, 
        Doamin.DTOs.ExecuteCollectorRequest request,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Execute all active pipelines in a collector sequentially
    /// </summary>
    Task<List<Doamin.DTOs.CollectorExecutionResult>> ExecuteAllPipelinesAsync(
        Guid tenantId,
        Guid collectorId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Dry run - validate and simulate execution without making actual API calls
    /// </summary>
    Task<Doamin.DTOs.CollectorExecutionResult> DryRunAsync(
        Guid tenantId,
        Guid collectorId,
        Guid pipelineId,
        Dictionary<string, object>? parameters,
        CancellationToken cancellationToken = default);
    
    // ============================================================================
    // Validation
    // ============================================================================
    
    /// <summary>
    /// Validate collector configuration before saving
    /// Checks if DataSource exists, functions are valid, parameter mappings are correct
    /// </summary>
    Task<ValidateCollectorResult> ValidateAsync(
        Guid tenantId,
        ValidateCollectorRequest request,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validate a single pipeline configuration
    /// </summary>
    Task<ValidationDetailsDto> ValidatePipelineAsync(
        Guid tenantId,
        Guid dataSourceId,
        CreatePipelineRequest request,
        CancellationToken cancellationToken = default);
    
    // ============================================================================
    // Stage Management & Promotion
    // ============================================================================
    
    /// <summary>
    /// Promote collector to next stage (Draft → Dev → Stage → Production)
    /// May trigger approval workflow depending on stage transition
    /// </summary>
    Task<PromoteCollectorResult> PromoteAsync(
        Guid tenantId,
        Guid collectorId,
        PromoteCollectorRequest request,
        string promotedBy,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Demote collector to previous stage
    /// </summary>
    Task<PromoteCollectorResult> DemoteAsync(
        Guid tenantId,
        Guid collectorId,
        string demotedBy,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Clone collector to create a new version
    /// </summary>
    Task<CollectorDetailDto> CloneAsync(
        Guid tenantId,
        Guid collectorId,
        CloneCollectorRequest request,
        string createdBy,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all versions of a collector
    /// </summary>
    Task<IEnumerable<CollectorVersionDto>> GetVersionsAsync(
        Guid tenantId,
        Guid collectorId,
        CancellationToken cancellationToken = default);
    
    // ============================================================================
    // Statistics & Monitoring
    // ============================================================================
    
    /// <summary>
    /// Get execution statistics for a collector
    /// </summary>
    Task<CollectorStatsDto> GetStatsAsync(
        Guid tenantId,
        Guid collectorId,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get collectors by stage
    /// </summary>
    Task<IEnumerable<CollectorDto>> GetByStageAsync(
        Guid tenantId,
        CollectorStage stage,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get collectors using a specific DataSource
    /// </summary>
    Task<IEnumerable<CollectorDto>> GetByDataSourceAsync(
        Guid tenantId,
        Guid dataSourceId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Search collectors by name or description
    /// </summary>
    Task<IEnumerable<CollectorDto>> SearchAsync(
        Guid tenantId,
        string searchTerm,
        CancellationToken cancellationToken = default);
}
