using DataCollector.Domain.Enums;
namespace DataCollector.Domain.Entities;

/// <summary>
/// Enhanced DataSource entity that stores complete API configuration
/// including functions, headers, authentication, rate limits, etc.
/// </summary>
public class DataSource : TenantEntity
{
    // Basic Information
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string? ImageUrl { get; set; }
    
    // Protocol and Type
    public DataSourceProtocol Protocol { get; set; }
    public DataSourceType Type { get; set; }
    
    // Source Information (for generated DataSources)
    public string? Source { get; set; } // URL or file content for Swagger/GraphQL/SOAP
    public string? BaseUrl { get; set; } // Can be overridden from source
    
    // Configuration Fields (JSON)
    // Stores dynamic configuration like environment, clientId, etc.
    public string? ConfigFields { get; set; } // JSON: List<ConfigField>
    
    // Authentication Configuration (JSON)
    // Stores auth type, details, requiresTLS, token expiration, etc.
    public string? AuthConfig { get; set; } // JSON: AuthConfiguration
    
    // Headers (JSON)
    // Stores static and dynamic headers
    public string? Headers { get; set; } // JSON: List<HeaderDefinition>
    
    // Functions (JSON) - CRITICAL FOR PIPELINE EXECUTION
    // Stores all available API functions/operations with their configurations
    public string? Functions { get; set; } // JSON: List<FunctionDefinition>
    
    // Rate Limiting (JSON)
    public string? RateLimitConfig { get; set; } // JSON: RateLimitConfiguration
    
    // Caching (JSON)
    public string? CacheConfig { get; set; } // JSON: CacheConfiguration
    
    // Retry Logic (JSON)
    public string? RetryConfig { get; set; } // JSON: RetryConfiguration
    
    // Monitoring (JSON)
    public string? MonitoringConfig { get; set; } // JSON: MonitoringConfiguration
    
    // Circuit Breaker (JSON)
    public string? CircuitBreakerConfig { get; set; } // JSON: CircuitBreakerConfiguration
    
    // Metadata
    public string Category { get; set; } = string.Empty;
    public string? Tags { get; set; } // JSON: List<string>
    public string? Metadata { get; set; } // JSON: Dictionary<string, object>
    
    // Status
    public bool IsActive { get; set; } = true;
    public DateTime? LastTestedAt { get; set; }
    public bool? LastTestResult { get; set; }
    public string? LastTestError { get; set; }
    
    // Navigation Properties
    public virtual ICollection<Pipeline> Pipelines { get; set; } = new List<Pipeline>();
}

// Supporting Classes for JSON Serialization

public class ConfigField
{
    public string Name { get; set; } = string.Empty;
    public int Type { get; set; } // ConfigFieldType enum value
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Required { get; set; }
    public string? Default { get; set; }
    public bool Encrypted { get; set; }
    public List<ConfigFieldOption>? Options { get; set; }
}

public class ConfigFieldOption
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class AuthConfiguration
{
    public string Type { get; set; } = "None"; // None, ApiKey, Basic, Bearer, OAuth2, Custom
    public Dictionary<string, object>? Details { get; set; }
    public bool RequiresTLS { get; set; } = true;
    public int? TokenExpirationMinutes { get; set; }
    public bool RefreshTokenSupported { get; set; }
}

public class HeaderDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool Required { get; set; }
    public bool IsDynamic { get; set; } // If true, value contains template like {config.clientId}
}

/// <summary>
/// Represents a function/operation available in the DataSource
/// This is what pipelines will reference for execution
/// </summary>
public class FunctionDefinition
{
    public string Id { get; set; } = string.Empty; // Function ID for reference
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Method { get; set; } = "GET"; // GET, POST, PUT, DELETE, PATCH
    public string Path { get; set; } = string.Empty;
    
    // Parameters
    public List<FunctionParameter>? Parameters { get; set; }
    
    // Request Body (for POST, PUT, PATCH)
    public FunctionRequestBody? RequestBody { get; set; }
    
    // Response
    public FunctionResponse? Response { get; set; }
    
    // Function-specific settings
    public bool RequiresAuth { get; set; } = true;
    public List<string>? Scopes { get; set; }
    public FunctionTimeout? Timeout { get; set; }
}

public class FunctionParameter
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "string"; // string, number, boolean, array, object
    public string Location { get; set; } = "query"; // query, path, header, body
    public bool Required { get; set; }
    public string? Default { get; set; }
    public string? Description { get; set; }
    public object? Schema { get; set; } // JSON Schema
}

public class FunctionRequestBody
{
    public object? Schema { get; set; } // JSON Schema
    public string ContentType { get; set; } = "application/json";
    public bool Required { get; set; }
}

public class FunctionResponse
{
    public string ExpectedFormat { get; set; } = "application/json";
    public object? Schema { get; set; } // JSON Schema
    public Dictionary<string, ResponseStatusDefinition>? StatusCodes { get; set; }
}

public class ResponseStatusDefinition
{
    public string Description { get; set; } = string.Empty;
    public object? Schema { get; set; }
}

public class FunctionTimeout
{
    public long Ticks { get; set; }
}

public class RateLimitConfiguration
{
    public int? RequestsPerMinute { get; set; }
    public int? RequestsPerHour { get; set; }
    public int? RequestsPerDay { get; set; }
    public string Strategy { get; set; } = "sliding_window"; // sliding_window, token_bucket, fixed_window
}

public class CacheConfiguration
{
    public bool Enabled { get; set; }
    public int TtlSeconds { get; set; } = 300;
    public List<string>? CacheableOperations { get; set; }
    public string Strategy { get; set; } = "memory"; // memory, redis, distributed
}

public class RetryConfiguration
{
    public bool Enabled { get; set; }
    public int MaxAttempts { get; set; } = 3;
    public int InitialDelayMs { get; set; } = 1000;
    public int MaxDelayMs { get; set; } = 30000;
    public string BackoffStrategy { get; set; } = "exponential"; // exponential, linear, constant
    public List<int>? RetryableStatusCodes { get; set; }
    public bool RetryOnTimeout { get; set; } = true;
}

public class MonitoringConfiguration
{
    public bool Enabled { get; set; }
    public bool EnableMetrics { get; set; }
    public bool EnableTracing { get; set; }
    public bool EnableHealthChecks { get; set; }
    public int MetricsIntervalSeconds { get; set; } = 30;
    public LoggingConfiguration? Logging { get; set; }
    public AlertingConfiguration? Alerting { get; set; }
}

public class LoggingConfiguration
{
    public bool LogRequests { get; set; }
    public bool LogResponses { get; set; }
    public bool LogErrors { get; set; } = true;
    public bool LogPerformance { get; set; }
    public string LogLevel { get; set; } = "INFO";
    public bool SanitizeSensitiveData { get; set; } = true;
    public List<string>? SensitiveFields { get; set; }
}

public class AlertingConfiguration
{
    public bool Enabled { get; set; }
    public double ErrorRateThreshold { get; set; } = 0.05;
    public int LatencyThresholdMs { get; set; } = 5000;
    public double AvailabilityThreshold { get; set; } = 0.99;
}

public class CircuitBreakerConfiguration
{
    public bool Enabled { get; set; }
    public int FailureThreshold { get; set; } = 5;
    public int SuccessThreshold { get; set; } = 3;
    public int TimeoutMs { get; set; } = 10000;
    public int ResetTimeoutMs { get; set; } = 60000;
    public int Strategy { get; set; } = 1; // Enum value
}
