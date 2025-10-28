using DataCollector.Application.DTOs;

namespace DataCollector.Application.Interfaces;

/// <summary>
/// Service for managing DataSources with complete API configuration
/// Supports manual creation and generation from Swagger/GraphQL/WSDL
/// </summary>
public interface IDataSourceService
{
    // ============================================================================
    // Manual DataSource Creation (REST, etc.)
    // ============================================================================
    
    /// <summary>
    /// Create a DataSource manually with complete configuration
    /// Used for REST APIs where you define endpoints, headers, auth, functions manually
    /// </summary>
    Task<DataSourceDetailDto> CreateManualAsync(
        Guid tenantId, 
        CreateManualDataSourceRequest request, 
        string createdBy,
        CancellationToken cancellationToken = default);
    
    // ============================================================================
    // Generate DataSource from External Sources
    // ============================================================================
    
    /// <summary>
    /// Generate DataSource from Swagger/OpenAPI URL
    /// Automatically discovers all operations and creates functions
    /// </summary>
    Task<DataSourceDetailDto> GenerateFromSwaggerUrlAsync(
        Guid tenantId,
        GenerateDataSourceFromUrlRequest request,
        string createdBy,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generate DataSource from Swagger/OpenAPI file content
    /// </summary>
    Task<DataSourceDetailDto> GenerateFromSwaggerContentAsync(
        Guid tenantId,
        GenerateDataSourceFromContentRequest request,
        string createdBy,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generate DataSource from GraphQL endpoint with introspection
    /// </summary>
    Task<DataSourceDetailDto> GenerateFromGraphQLAsync(
        Guid tenantId,
        GenerateDataSourceFromUrlRequest request,
        string createdBy,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generate DataSource from SOAP WSDL
    /// </summary>
    Task<DataSourceDetailDto> GenerateFromWsdlAsync(
        Guid tenantId,
        GenerateDataSourceFromUrlRequest request,
        string createdBy,
        CancellationToken cancellationToken = default);
    
    // ============================================================================
    // CRUD Operations
    // ============================================================================
    
    /// <summary>
    /// Get DataSource by ID with full details
    /// </summary>
    Task<DataSourceDetailDto> GetByIdAsync(
        Guid tenantId, 
        Guid id, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all DataSources for a tenant (summary view)
    /// </summary>
    Task<IEnumerable<DataSourceDto>> GetAllAsync(
        Guid tenantId, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update DataSource configuration
    /// </summary>
    Task<DataSourceDetailDto> UpdateAsync(
        Guid tenantId, 
        Guid id, 
        CreateManualDataSourceRequest request, 
        string updatedBy,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete DataSource (soft delete)
    /// Cannot delete if pipelines are using it
    /// </summary>
    Task<bool> DeleteAsync(
        Guid tenantId, 
        Guid id, 
        string deletedBy,
        CancellationToken cancellationToken = default);
    
    // ============================================================================
    // Function Management
    // ============================================================================
    
    /// <summary>
    /// Get all functions available in a DataSource
    /// </summary>
    Task<IEnumerable<DataSourceFunctionDto>> GetFunctionsAsync(
        Guid tenantId,
        Guid dataSourceId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a specific function from a DataSource
    /// </summary>
    Task<FunctionDefinitionDto> GetFunctionAsync(
        Guid tenantId,
        Guid dataSourceId,
        string functionId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Add a new function to an existing DataSource
    /// </summary>
    Task<FunctionDefinitionDto> AddFunctionAsync(
        Guid tenantId,
        Guid dataSourceId,
        FunctionDefinitionDto function,
        string updatedBy,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update an existing function in a DataSource
    /// </summary>
    Task<FunctionDefinitionDto> UpdateFunctionAsync(
        Guid tenantId,
        Guid dataSourceId,
        string functionId,
        FunctionDefinitionDto function,
        string updatedBy,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Remove a function from a DataSource
    /// Cannot remove if pipelines are using it
    /// </summary>
    Task<bool> RemoveFunctionAsync(
        Guid tenantId,
        Guid dataSourceId,
        string functionId,
        string updatedBy,
        CancellationToken cancellationToken = default);
    
    // ============================================================================
    // Testing & Validation
    // ============================================================================
    
    /// <summary>
    /// Test connection to DataSource
    /// Optionally test a specific function
    /// </summary>
    Task<TestDataSourceResult> TestConnectionAsync(
        Guid tenantId, 
        TestDataSourceRequest request,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validate a source before generating DataSource
    /// </summary>
    Task<ValidateSourceResult> ValidateSourceAsync(
        ValidateSourceRequest request,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get available operations from a source (preview before generating)
    /// </summary>
    Task<IEnumerable<string>> GetAvailableOperationsAsync(
        DataSourceType sourceType,
        string source,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Preview generated DataSource without saving
    /// </summary>
    Task<DataSourceDetailDto> PreviewGeneratedDataSourceAsync(
        GenerateDataSourceFromUrlRequest request,
        CancellationToken cancellationToken = default);
    
    // ============================================================================
    // Statistics & Analytics
    // ============================================================================
    
    /// <summary>
    /// Get usage statistics for a DataSource
    /// </summary>
    Task<DataSourceStatsDto> GetStatsAsync(
        Guid tenantId,
        Guid dataSourceId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get DataSources by category
    /// </summary>
    Task<IEnumerable<DataSourceDto>> GetByCategoryAsync(
        Guid tenantId,
        string category,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Search DataSources by name, description, or tags
    /// </summary>
    Task<IEnumerable<DataSourceDto>> SearchAsync(
        Guid tenantId,
        string searchTerm,
        CancellationToken cancellationToken = default);
}
