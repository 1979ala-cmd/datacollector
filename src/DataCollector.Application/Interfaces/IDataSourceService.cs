using DataCollector.Application.DTOs;

namespace DataCollector.Application.Interfaces;

/// <summary>
/// Service interface for DataSource management
/// Supports manual creation and generation from various sources
/// </summary>
public interface IDataSourceService
{
    // ==================== MANUAL DATASOURCE CREATION ====================
    
    /// <summary>
    /// Create a DataSource manually by defining all configuration
    /// </summary>
    Task<DataSourceResponseDto> CreateManualAsync(
        Guid tenantId, 
        CreateManualDataSourceRequest request, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update an existing DataSource
    /// </summary>
    Task<DataSourceResponseDto> UpdateAsync(
        Guid tenantId, 
        Guid dataSourceId, 
        CreateManualDataSourceRequest request, 
        CancellationToken cancellationToken = default);
    
    // ==================== DATASOURCE GENERATION ====================
    
    /// <summary>
    /// Generate DataSource from Swagger/OpenAPI URL
    /// </summary>
    Task<DataSourceResponseDto> GenerateFromSwaggerUrlAsync(
        Guid tenantId, 
        GenerateDataSourceFromUrlRequest request, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generate DataSource from Swagger/OpenAPI file content
    /// </summary>
    Task<DataSourceResponseDto> GenerateFromSwaggerContentAsync(
        Guid tenantId, 
        GenerateDataSourceFromContentRequest request, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generate DataSource from GraphQL endpoint (with introspection)
    /// </summary>
    Task<DataSourceResponseDto> GenerateFromGraphQLAsync(
        Guid tenantId, 
        GenerateDataSourceFromUrlRequest request, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generate DataSource from SOAP WSDL
    /// </summary>
    Task<DataSourceResponseDto> GenerateFromWsdlAsync(
        Guid tenantId, 
        GenerateDataSourceFromUrlRequest request, 
        CancellationToken cancellationToken = default);
    
    // ==================== DATASOURCE RETRIEVAL ====================
    
    /// <summary>
    /// Get DataSource by ID
    /// </summary>
    Task<DataSourceResponseDto> GetByIdAsync(
        Guid tenantId, 
        Guid dataSourceId, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all DataSources for a tenant
    /// </summary>
    Task<List<DataSourceResponseDto>> GetAllAsync(
        Guid tenantId, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get DataSources by category
    /// </summary>
    Task<List<DataSourceResponseDto>> GetByCategoryAsync(
        Guid tenantId, 
        string category, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get DataSources by protocol
    /// </summary>
    Task<List<DataSourceResponseDto>> GetByProtocolAsync(
        Guid tenantId, 
        int protocol, 
        CancellationToken cancellationToken = default);
    
    // ==================== FUNCTION MANAGEMENT ====================
    
    /// <summary>
    /// Get all functions for a DataSource
    /// </summary>
    Task<List<FunctionDefinitionDto>> GetFunctionsAsync(
        Guid tenantId, 
        Guid dataSourceId, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a specific function by ID
    /// </summary>
    Task<FunctionDefinitionDto> GetFunctionByIdAsync(
        Guid tenantId, 
        Guid dataSourceId, 
        string functionId, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Add a new function to a DataSource
    /// </summary>
    Task<FunctionDefinitionDto> AddFunctionAsync(
        Guid tenantId, 
        Guid dataSourceId, 
        FunctionDefinitionDto function, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update an existing function
    /// </summary>
    Task<FunctionDefinitionDto> UpdateFunctionAsync(
        Guid tenantId, 
        Guid dataSourceId, 
        string functionId, 
        FunctionDefinitionDto function, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete a function
    /// </summary>
    Task DeleteFunctionAsync(
        Guid tenantId, 
        Guid dataSourceId, 
        string functionId, 
        CancellationToken cancellationToken = default);
    
    // ==================== VALIDATION & TESTING ====================
    
    /// <summary>
    /// Validate a source before generating DataSource
    /// </summary>
    Task<bool> ValidateSourceAsync(
        ValidateDataSourceRequest request, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Analyze source to get metadata
    /// </summary>
    Task<object> AnalyzeSourceAsync(
        AnalyzeDataSourceRequest request, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get available operations from source
    /// </summary>
    Task<List<string>> GetOperationsFromSourceAsync(
        GetOperationsRequest request, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Preview generated DataSource without saving
    /// </summary>
    Task<DataSourceResponseDto> PreviewDataSourceAsync(
        PreviewDataSourceRequest request, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Test a DataSource function with sample parameters
    /// </summary>
    Task<object> TestFunctionAsync(
        Guid tenantId, 
        Guid dataSourceId, 
        string functionId, 
        Dictionary<string, object> parameters, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Test DataSource connection
    /// </summary>
    Task<bool> TestConnectionAsync(
        Guid tenantId, 
        Guid dataSourceId, 
        CancellationToken cancellationToken = default);
    
    // ==================== LIFECYCLE MANAGEMENT ====================
    
    /// <summary>
    /// Activate a DataSource
    /// </summary>
    Task ActivateAsync(
        Guid tenantId, 
        Guid dataSourceId, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deactivate a DataSource
    /// </summary>
    Task DeactivateAsync(
        Guid tenantId, 
        Guid dataSourceId, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete a DataSource (soft delete)
    /// </summary>
    Task DeleteAsync(
        Guid tenantId, 
        Guid dataSourceId, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Clone a DataSource
    /// </summary>
    Task<DataSourceResponseDto> CloneAsync(
        Guid tenantId, 
        Guid dataSourceId, 
        string newName, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Export DataSource as JSON
    /// </summary>
    Task<string> ExportAsync(
        Guid tenantId, 
        Guid dataSourceId, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Import DataSource from JSON
    /// </summary>
    Task<DataSourceResponseDto> ImportAsync(
        Guid tenantId, 
        string dataSourceJson, 
        CancellationToken cancellationToken = default);
}