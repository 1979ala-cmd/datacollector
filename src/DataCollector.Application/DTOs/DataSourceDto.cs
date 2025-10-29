using DataCollector.Domain.Entities;
using System;
using System.Collections.Generic;

namespace DataCollector.Application.DTOs;

// ==================== DATA SOURCE DTOs ====================

/// <summary>
/// DTO for DataSource response (matches datasource_structure.txt)
/// </summary>
public record DataSourceResponseDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Version { get; init; } = "1.0.0";
    public string? DatasourceVersion { get; init; }
    public string? ImageUrl { get; init; }
    public int Protocol { get; init; }
    public ConfigRoot Config { get; init; } = new();
    public List<HeaderDefinition>? Headers { get; init; }
    public BodyConfiguration? Body { get; init; }
    public List<FunctionDefinition> Functions { get; init; } = new();
    public string Category { get; init; } = string.Empty;
    public List<string>? Tags { get; init; }
    public MetadataInfo? Metadata { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

/// <summary>
/// Request to create a manual DataSource
/// </summary>
public record CreateManualDataSourceRequest
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Version { get; init; } = "1.0.0";
    public string? ImageUrl { get; init; }
    public int Protocol { get; init; } // 0=REST, 1=GraphQL, 2=SOAP, etc.
    
    // Configuration
    public List<ConfigFieldDto> ConfigFields { get; init; } = new();
    public AuthConfigurationDto Auth { get; init; } = new();
    public RateLimitConfigurationDto? RateLimit { get; init; }
    public CacheConfigurationDto? Cache { get; init; }
    public RetryConfigurationDto? Retry { get; init; }
    public MonitoringConfigurationDto? Monitoring { get; init; }
    public CircuitBreakerConfigurationDto? CircuitBreaker { get; init; }
    
    // Headers
    public List<HeaderDefinitionDto>? Headers { get; init; }
    
    // Body configuration
    public BodyConfigurationDto? Body { get; init; }
    
    // Functions
    public List<FunctionDefinitionDto> Functions { get; init; } = new();
    
    // Metadata
    public string Category { get; init; } = string.Empty;
    public List<string>? Tags { get; init; }
    public MetadataInfoDto? Metadata { get; init; }
}

/// <summary>
/// Request to generate DataSource from URL (Swagger, GraphQL, WSDL)
/// </summary>
public record GenerateDataSourceFromUrlRequest
{
    public int SourceType { get; init; } // 1=SwaggerUrl, 3=GraphQL, 4=SoapWsdl
    public string Source { get; init; } = string.Empty; // URL
    public string DataSourceName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? BaseUrlOverride { get; init; }
    public bool FilterOperations { get; init; }
    public List<string>? IncludedOperations { get; init; }
    public bool OverrideAuth { get; init; }
    public string? CustomAuthType { get; init; }
    public bool GenerateModels { get; init; }
    public bool ValidateRequests { get; init; }
    public int DefaultRateLimit { get; init; } = 60;
}

/// <summary>
/// Request to generate DataSource from file content
/// </summary>
public record GenerateDataSourceFromContentRequest
{
    public int SourceType { get; init; } // 2=SwaggerFile
    public string Source { get; init; } = string.Empty; // File content (JSON/YAML)
    public string DataSourceName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool GenerateModels { get; init; }
    public bool ValidateRequests { get; init; }
    public int DefaultRateLimit { get; init; } = 100;
}

// ==================== CONFIGURATION DTOs ====================

public record ConfigFieldDto
{
    public string Name { get; init; } = string.Empty;
    public int Type { get; init; }
    public string Label { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool Required { get; init; }
    public string Default { get; init; } = string.Empty;
    public List<ConfigFieldOptionDto> Options { get; init; } = new();
    public ValidationRulesDto Validation { get; init; } = new();
    public bool Encrypted { get; init; }
    public string? DependsOn { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
}

public record ConfigFieldOptionDto
{
    public string Value { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string? Description { get; init; }
}

public record ValidationRulesDto
{
    public string? Pattern { get; init; }
    public int? Min { get; init; }
    public int? Max { get; init; }
    public int? MinLength { get; init; }
    public int? MaxLength { get; init; }
    public List<string> AllowedValues { get; init; } = new();
    public string? CustomValidator { get; init; }
}

public record AuthConfigurationDto
{
    public string Type { get; init; } = string.Empty;
    public AuthDetailsDto Details { get; init; } = new();
    public bool RequiresTLS { get; init; } = true;
    public int TokenExpirationMinutes { get; init; } = 60;
    public bool RefreshTokenSupported { get; init; }
}

public record AuthDetailsDto
{
    public string KeyName { get; init; } = string.Empty;
    public string Placement { get; init; } = "header";
    public string Value { get; init; } = string.Empty;
}

public record RateLimitConfigurationDto
{
    public int RequestsPerMinute { get; init; }
    public int RequestsPerHour { get; init; }
    public int RequestsPerDay { get; init; }
    public string Strategy { get; init; } = "fixed";
}

public record CacheConfigurationDto
{
    public bool Enabled { get; init; }
    public int TtlSeconds { get; init; }
    public List<string> CacheableOperations { get; init; } = new();
    public string Strategy { get; init; } = "memory";
}

public record RetryConfigurationDto
{
    public bool Enabled { get; init; }
    public int MaxAttempts { get; init; }
    public int InitialDelayMs { get; init; }
    public string BackoffStrategy { get; init; } = "exponential";
    public List<int> RetryableStatusCodes { get; init; } = new();
}

public record MonitoringConfigurationDto
{
    public bool Enabled { get; init; }
    public bool EnableMetrics { get; init; }
    public bool EnableTracing { get; init; }
    public bool EnableHealthChecks { get; init; }
    public bool MetricsEnabled { get; init; }
    public bool HealthCheckEnabled { get; init; }
    public int MetricsIntervalSeconds { get; init; }
    public string MetricsEndpoint { get; init; } = "/metrics";
    public string HealthCheckEndpoint { get; init; } = "/health";
    public string HealthCheckPath { get; init; } = "/health";
    public List<string> CustomMetrics { get; init; } = new();
    public LoggingConfigurationDto Logging { get; init; } = new();
    public AlertingConfigurationDto Alerting { get; init; } = new();
}

public record LoggingConfigurationDto
{
    public bool LogRequests { get; init; }
    public bool LogResponses { get; init; }
    public bool LogErrors { get; init; }
    public bool LogPerformance { get; init; }
    public string LogLevel { get; init; } = "Information";
    public bool SanitizeSensitiveData { get; init; }
    public List<string> SensitiveFields { get; init; } = new();
}

public record AlertingConfigurationDto
{
    public bool Enabled { get; init; }
    public List<object> Rules { get; init; } = new();
    public NotificationConfigurationDto Notifications { get; init; } = new();
    public double ErrorRateThreshold { get; init; }
    public int LatencyThresholdMs { get; init; }
    public double AvailabilityThreshold { get; init; }
}

public record NotificationConfigurationDto
{
    public List<string> Channels { get; init; } = new();
    public Dictionary<string, object> Settings { get; init; } = new();
}

public record CircuitBreakerConfigurationDto
{
    public bool Enabled { get; init; }
    public int FailureThreshold { get; init; }
    public int SuccessThreshold { get; init; }
    public int TimeoutMs { get; init; }
    public int ResetTimeoutMs { get; init; }
    public int RecoveryTimeoutMs { get; init; }
    public List<int> TrackedStatusCodes { get; init; } = new();
    public List<string> TrackedExceptions { get; init; } = new();
    public bool LogStateChanges { get; init; }
    public int Strategy { get; init; }
}

public record HeaderDefinitionDto
{
    public string Name { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public bool Required { get; init; }
    public bool IsDynamic { get; init; }
}

public record BodyConfigurationDto
{
    public string DefaultFormat { get; init; } = string.Empty;
    public Dictionary<string, object> Templates { get; init; } = new();
    public Dictionary<string, object> Schemas { get; init; } = new();
}

// ==================== FUNCTION DTOs ====================

public record FunctionDefinitionDto
{
    public string Id { get; init; } = "00000000-0000-0000-0000-000000000000";
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Method { get; init; } = "GET";
    public string Path { get; init; } = string.Empty;
    public List<FunctionParameterDto> Parameters { get; init; } = new();
    public FunctionRequestBodyDto RequestBody { get; init; } = new();
    public FunctionResponseDto Response { get; init; } = new();
    public Dictionary<string, object> ProtocolSpecific { get; init; } = new();
    public bool RequiresAuth { get; init; } = true;
    public List<string> Scopes { get; init; } = new();
    public TimeSpan? Timeout { get; init; }
    public bool IsDeprecated { get; init; }
    public string? DeprecationMessage { get; init; }
}

public record FunctionParameterDto
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = "string";
    public string Location { get; init; } = "query";
    public bool Required { get; init; }
    public string? Description { get; init; }
    public string? Default { get; init; }
    public ValidationRulesDto Validation { get; init; } = new();
    public List<string> Examples { get; init; } = new();
}

public record FunctionRequestBodyDto
{
    public Dictionary<string, object> Schema { get; init; } = new();
    public string? TemplateRef { get; init; }
    public string ContentType { get; init; } = "application/json";
    public bool Required { get; init; }
}

public record FunctionResponseDto
{
    public string ExpectedFormat { get; init; } = string.Empty;
    public Dictionary<string, object> Schema { get; init; } = new();
    public Dictionary<string, ResponseStatusCodeDto> StatusCodes { get; init; } = new();
    public List<HeaderDefinitionDto> Headers { get; init; } = new();
}

public record ResponseStatusCodeDto
{
    public string Description { get; init; } = string.Empty;
    public Dictionary<string, object>? Schema { get; init; }
}

public record MetadataInfoDto
{
    public string? Documentation { get; init; }
    public string? SupportContact { get; init; }
    public List<string> Dependencies { get; init; } = new();
    public Dictionary<string, object> Compatibility { get; init; } = new();
    public bool BetaFeature { get; init; }
    public string? ChangeLog { get; init; }
    public List<string> KnownIssues { get; init; } = new();
}

// ==================== HELPER/VALIDATION DTOs ====================

public record ValidateDataSourceRequest
{
    public int SourceType { get; init; }
    public string Source { get; init; } = string.Empty;
}

public record AnalyzeDataSourceRequest
{
    public int SourceType { get; init; }
    public string Source { get; init; } = string.Empty;
}

public record GetOperationsRequest
{
    public int SourceType { get; init; }
    public string Source { get; init; } = string.Empty;
}

public record PreviewDataSourceRequest
{
    public int SourceType { get; init; }
    public string Source { get; init; } = string.Empty;
    public string DataSourceName { get; init; } = string.Empty;
}