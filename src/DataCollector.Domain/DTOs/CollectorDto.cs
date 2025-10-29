using DataCollector.Domain.Enums;
namespace DataCollector.Domain.DTOs;

// ============================================================================
// Collector DTOs
// ============================================================================

public record CollectorDto(
    Guid Id,
    string Name,
    string Description,
    CollectorStage Stage,
    bool IsActive,
    DateTime CreatedAt,
    int PipelineCount,
    Guid? DataSourceId
);

public record CollectorDetailDto(
    Guid Id,
    string Name,
    string Description,
    int Version,
    CollectorStage Stage,
    bool IsActive,
    Guid? DataSourceId,
    DataSourceDto? DataSource, // Include DataSource details
    List<PipelineDetailDto> Pipelines,
    ApprovalStatus? ApprovalStatus,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string CreatedBy,
    string? UpdatedBy
);

// ============================================================================
// Create/Update Requests - UPDATED
// ============================================================================

/// <summary>
/// Request to create a new collector with pipelines
/// All pipelines must use the same DataSource
/// </summary>
public record CreateCollectorRequest(
    string Name,
    string Description,
    Guid DataSourceId, // SINGLE DataSource for all pipelines
    List<CreatePipelineRequest> Pipelines
);

/// <summary>
/// Request to create a pipeline - UPDATED to use function references
/// </summary>
public record CreatePipelineRequest(
    string Name,
    string? Description,
    string FunctionId, // References a function from DataSource.Functions
    Dictionary<string, string>? ParameterMappings, // Maps data from previous steps
    Dictionary<string, object>? StaticParameters, // Hardcoded values
    DataIngestionConfigurationDto? DataIngestion,
    List<CreateProcessingStepRequest> ProcessingSteps
);

public record CreateProcessingStepRequest(
    string Name,
    string Type, // api-call, pagination, retry, filter, for-each, transform, etc.
    bool Enabled,
    Dictionary<string, object>? Config,
    List<CreateProcessingStepRequest>? ChildSteps
);

public record UpdateCollectorRequest(
    string? Name,
    string? Description,
    bool? IsActive
);

// ============================================================================
// Pipeline DTOs - UPDATED
// ============================================================================

public record PipelineDto(
    Guid Id,
    string Name,
    string? Description,
    Guid DataSourceId,
    string FunctionId,
    string FunctionName, // From DataSource function
    string Method, // From DataSource function
    string ApiPath, // From DataSource function
    bool IsEnabled,
    int ProcessingStepCount
);

public record PipelineDetailDto(
    Guid Id,
    string Name,
    string? Description,
    Guid DataSourceId,
    string FunctionId,
    FunctionDefinitionDto Function, // Full function details from DataSource
    Dictionary<string, string>? ParameterMappings,
    Dictionary<string, object>? StaticParameters,
    DataIngestionConfigurationDto? DataIngestion,
    bool IsEnabled,
    List<ProcessingStepDetailDto> ProcessingSteps,
    DateTime CreatedAt
);

public record ProcessingStepDetailDto(
    Guid Id,
    string Name,
    ProcessingStepType Type,
    int Order,
    bool IsEnabled,
    Dictionary<string, object>? Config,
    List<ProcessingStepDetailDto>? ChildSteps
);

// ============================================================================
// Data Ingestion Configuration
// ============================================================================

public record DataIngestionConfigurationDto(
    string Strategy, // full-sync, incremental, on-demand
    string? SyncMode, // full, incremental
    int BatchSize,
    bool Parallelization,
    IncrementalConfigurationDto? IncrementalConfig,
    string ConflictResolution
);

public record IncrementalConfigurationDto(
    bool Enabled,
    string DeltaStrategy, // created, updated, both, deleted
    string TrackingField,
    string TrackingFieldType, // timestamp, version, cursor
    int LookbackWindow,
    string? LastSyncValue
);

// ============================================================================
// Execution DTOs - UPDATED
// ============================================================================

public record ExecuteCollectorRequest(
    Guid PipelineId,
    Dictionary<string, object>? Parameters, // Runtime parameter overrides
    bool DryRun = false // If true, validate but don't execute
);

public record CollectorExecutionResult(
    Guid ExecutionId,
    Guid CollectorId,
    Guid PipelineId,
    bool Success,
    string Message,
    ExecutionDetailsDto? Details,
    DateTime StartedAt,
    DateTime? CompletedAt,
    int RecordsProcessed,
    List<StepExecutionResultDto>? StepResults
);

public record ExecutionDetailsDto(
    string FunctionId,
    string FunctionName,
    string Method,
    string ApiPath,
    Dictionary<string, object>? ResolvedParameters,
    int? ResponseStatusCode,
    object? ResponseData,
    int ResponseTime,
    string? ErrorMessage
);

public record StepExecutionResultDto(
    Guid StepId,
    string StepName,
    ProcessingStepType StepType,
    bool Success,
    string? Message,
    int ExecutionTime,
    object? OutputData,
    List<StepExecutionResultDto>? ChildResults
);

// ============================================================================
// Validation DTOs
// ============================================================================

public record ValidateCollectorRequest(
    string Name,
    Guid DataSourceId,
    List<CreatePipelineRequest> Pipelines
);

public record ValidateCollectorResult(
    bool IsValid,
    List<string>? Errors,
    List<string>? Warnings,
    Dictionary<string, ValidationDetailsDto>? PipelineValidations
);

public record ValidationDetailsDto(
    bool IsValid,
    List<string>? Errors,
    List<string>? Warnings,
    bool FunctionExists,
    bool AllParametersValid,
    bool AllStepsValid
);

// ============================================================================
// Collector Management DTOs
// ============================================================================

public record PromoteCollectorRequest(
    CollectorStage TargetStage,
    string? Comment
);

public record PromoteCollectorResult(
    bool Success,
    string Message,
    CollectorStage CurrentStage,
    CollectorStage? PreviousStage,
    bool RequiresApproval,
    Guid? ApprovalWorkflowId
);

public record CloneCollectorRequest(
    string NewName,
    string? Description,
    bool CloneAsDraft
);

public record CollectorVersionDto(
    int Version,
    CollectorStage Stage,
    DateTime CreatedAt,
    string CreatedBy,
    string? Comment
);

// ============================================================================
// Statistics & Monitoring
// ============================================================================

public record CollectorStatsDto(
    Guid CollectorId,
    int TotalExecutions,
    int SuccessfulExecutions,
    int FailedExecutions,
    double SuccessRate,
    DateTime? LastExecution,
    int AverageExecutionTime,
    int TotalRecordsProcessed,
    Dictionary<Guid, PipelineStatsDto>? PipelineStats
);

public record PipelineStatsDto(
    Guid PipelineId,
    string PipelineName,
    int TotalExecutions,
    int SuccessfulExecutions,
    int FailedExecutions,
    double SuccessRate,
    int AverageExecutionTime,
    int TotalRecordsProcessed
);

// ============================================================================
// DataSource DTO - ADDED (was missing)
// ============================================================================

/// <summary>
/// Basic DataSource information for collector details
/// </summary>
public record DataSourceDto(
    Guid Id,
    string Name,
    string Description,
    string Version,
    int Protocol, // 0=REST, 1=GraphQL, 2=SOAP
    string BaseUrl,
    bool IsActive,
    int FunctionCount,
    DateTime CreatedAt
);

// ============================================================================
// Function Definition DTO - ADDED (was missing)
// ============================================================================

/// <summary>
/// Function definition from DataSource
/// </summary>
public record FunctionDefinitionDto(
    string Id,
    string Name,
    string Description,
    string Method,
    string Path,
    List<FunctionParameterDto> Parameters,
    FunctionRequestBodyDto? RequestBody,
    FunctionResponseDto? Response,
    bool RequiresAuth,
    List<string>? Scopes,
    TimeSpan? Timeout,
    bool IsDeprecated,
    string? DeprecationMessage
);

public record FunctionParameterDto(
    string Name,
    string Type, // string, integer, boolean, etc.
    string Location, // query, path, header, body
    bool Required,
    string? Description,
    string? Default,
    ValidationRulesDto? Validation,
    List<string>? Examples
);


public record FunctionRequestBodyDto(
    Dictionary<string, object> Schema,
    string? TemplateRef,
    string ContentType,
    bool Required
);

public record FunctionResponseDto(
    string ExpectedFormat,
    Dictionary<string, object> Schema,
    Dictionary<string, ResponseStatusCodeDto>? StatusCodes,
    List<HeaderDefinitionDto>? Headers
);

public record ResponseStatusCodeDto(
    string Description,
    Dictionary<string, object>? Schema
);

public record HeaderDefinitionDto(
    string Name,
    string Value,
    bool Required,
    bool IsDynamic
);