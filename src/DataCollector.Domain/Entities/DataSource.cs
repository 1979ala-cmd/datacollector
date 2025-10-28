using DataCollector.Domain.Enums;

namespace DataCollector.Domain.Entities;

/// <summary>
/// DataSource entity that stores complete API configuration
/// Matches datasource_structure.txt specification exactly
/// </summary>
public class DataSource : TenantEntity
{
    // Basic Information
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string? DatasourceVersion { get; set; }
    public string? ImageUrl { get; set; }
    
    // Protocol (0=REST, 1=GraphQL, 2=SOAP, 3=gRPC, 4=WebSocket)
    public int Protocol { get; set; }
    
    // Configuration (JSON) - Stores ConfigRoot object
    public string Config { get; set; } = string.Empty; // JSON: ConfigRoot
    
    // Headers (JSON)
    public string? Headers { get; set; } // JSON: List<HeaderDefinition>
    
    // Body (JSON)
    public string? Body { get; set; } // JSON: BodyConfiguration
    
    // Functions (JSON) - CRITICAL FOR PIPELINE EXECUTION
    public string Functions { get; set; } = string.Empty; // JSON: List<FunctionDefinition>
    
    // Metadata
    public string Category { get; set; } = string.Empty;
    public string? Tags { get; set; } // JSON: List<string>
    public string? Metadata { get; set; } // JSON: MetadataInfo
    
    // Timestamps and Status
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    
    // Navigation Properties
    public virtual ICollection<Pipeline> Pipelines { get; set; } = new List<Pipeline>();
}

// ==================== CONFIGURATION CLASSES ====================
// These match datasource_structure.txt exactly

/// <summary>
/// Root configuration object
/// </summary>
public class ConfigRoot
{
    public List<ConfigField> Fields { get; set; } = new();
    public AuthConfiguration Auth { get; set; } = new();
    public RateLimitConfiguration RateLimit { get; set; } = new();
    public CacheConfiguration Cache { get; set; } = new();
    public RetryConfiguration Retry { get; set; } = new();
    public MonitoringConfiguration Monitoring { get; set; } = new();
    public CircuitBreakerConfiguration CircuitBreaker { get; set; } = new();
}

/// <summary>
/// Configuration field definition
/// </summary>
public class ConfigField
{
    public string Name { get; set; } = string.Empty;
    public int Type { get; set; } // Field type enum: 0=Text, 1=TextArea, 2=Password, etc.
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Required { get; set; }
    public string Default { get; set; } = string.Empty;
    public List<ConfigFieldOption> Options { get; set; } = new();
    public ValidationRules Validation { get; set; } = new();
    public bool Encrypted { get; set; }
    public string? DependsOn { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class ConfigFieldOption
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class ValidationRules
{
    public string? Pattern { get; set; }
    public int? Min { get; set; }
    public int? Max { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public List<string> AllowedValues { get; set; } = new();
    public string? CustomValidator { get; set; }
}

// ==================== AUTH CONFIGURATION ====================

public class AuthConfiguration
{
    public string Type { get; set; } = string.Empty; // "api_key", "oauth2", "basic", etc.
    public AuthDetails Details { get; set; } = new();
    public bool RequiresTLS { get; set; }
    public int TokenExpirationMinutes { get; set; }
    public bool RefreshTokenSupported { get; set; }
}

public class AuthDetails
{
    public string KeyName { get; set; } = string.Empty; // e.g., "Authorization"
    public string Placement { get; set; } = string.Empty; // "header", "query", "body"
    public string Value { get; set; } = string.Empty; // Template: "${config.api_key}"
}

// ==================== RATE LIMITING ====================

public class RateLimitConfiguration
{
    public int RequestsPerMinute { get; set; }
    public int RequestsPerHour { get; set; }
    public int RequestsPerDay { get; set; }
    public string Strategy { get; set; } = "fixed"; // "fixed", "sliding_window", "token_bucket"
}

// ==================== CACHE CONFIGURATION ====================

public class CacheConfiguration
{
    public bool Enabled { get; set; }
    public int TtlSeconds { get; set; }
    public List<string> CacheableOperations { get; set; } = new();
    public string Strategy { get; set; } = "memory"; // "memory", "redis", "distributed"
}

// ==================== RETRY CONFIGURATION ====================

public class RetryConfiguration
{
    public bool Enabled { get; set; }
    public int MaxAttempts { get; set; }
    public int InitialDelayMs { get; set; }
    public string BackoffStrategy { get; set; } = "exponential"; // "exponential", "linear", "constant"
    public List<int> RetryableStatusCodes { get; set; } = new();
}

// ==================== MONITORING CONFIGURATION ====================

public class MonitoringConfiguration
{
    public bool Enabled { get; set; }
    public bool EnableMetrics { get; set; }
    public bool EnableTracing { get; set; }
    public bool EnableHealthChecks { get; set; }
    public bool MetricsEnabled { get; set; }
    public bool HealthCheckEnabled { get; set; }
    public int MetricsIntervalSeconds { get; set; }
    public string MetricsEndpoint { get; set; } = "/metrics";
    public string HealthCheckEndpoint { get; set; } = "/health";
    public string HealthCheckPath { get; set; } = "/health";
    public List<string> CustomMetrics { get; set; } = new();
    public LoggingConfiguration Logging { get; set; } = new();
    public AlertingConfiguration Alerting { get; set; } = new();
}

public class LoggingConfiguration
{
    public bool LogRequests { get; set; }
    public bool LogResponses { get; set; }
    public bool LogErrors { get; set; }
    public bool LogPerformance { get; set; }
    public string LogLevel { get; set; } = "Information";
    public bool SanitizeSensitiveData { get; set; }
    public List<string> SensitiveFields { get; set; } = new();
}

public class AlertingConfiguration
{
    public bool Enabled { get; set; }
    public List<object> Rules { get; set; } = new();
    public NotificationConfiguration Notifications { get; set; } = new();
    public double ErrorRateThreshold { get; set; }
    public int LatencyThresholdMs { get; set; }
    public double AvailabilityThreshold { get; set; }
}

public class NotificationConfiguration
{
    public List<string> Channels { get; set; } = new();
    public Dictionary<string, object> Settings { get; set; } = new();
}

// ==================== CIRCUIT BREAKER ====================

public class CircuitBreakerConfiguration
{
    public bool Enabled { get; set; }
    public int FailureThreshold { get; set; }
    public int SuccessThreshold { get; set; }
    public int TimeoutMs { get; set; }
    public int ResetTimeoutMs { get; set; }
    public int RecoveryTimeoutMs { get; set; }
    public List<int> TrackedStatusCodes { get; set; } = new();
    public List<string> TrackedExceptions { get; set; } = new();
    public bool LogStateChanges { get; set; }
    public int Strategy { get; set; } // 0 or 1
}

// ==================== HEADERS ====================

public class HeaderDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool Required { get; set; }
    public bool IsDynamic { get; set; }
}

// ==================== BODY CONFIGURATION ====================

public class BodyConfiguration
{
    public string DefaultFormat { get; set; } = string.Empty;
    public Dictionary<string, object> Templates { get; set; } = new();
    public Dictionary<string, object> Schemas { get; set; } = new();
}

// ==================== FUNCTIONS ====================

/// <summary>
/// Represents a function/operation available in the DataSource
/// This is what pipelines will reference for execution
/// </summary>
public class FunctionDefinition
{
    public string Id { get; set; } = "00000000-0000-0000-0000-000000000000";
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Method { get; set; } = "GET";
    public string Path { get; set; } = string.Empty;
    public List<FunctionParameter> Parameters { get; set; } = new();
    public FunctionRequestBody RequestBody { get; set; } = new();
    public FunctionResponse Response { get; set; } = new();
    public Dictionary<string, object> ProtocolSpecific { get; set; } = new();
    public bool RequiresAuth { get; set; } = true;
    public List<string> Scopes { get; set; } = new();
    public TimeSpan? Timeout { get; set; }
    public bool IsDeprecated { get; set; }
    public string? DeprecationMessage { get; set; }
}

public class FunctionParameter
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "string";
    public string Location { get; set; } = "query"; // "query", "path", "header", "body"
    public bool Required { get; set; }
    public string? Description { get; set; }
    public string? Default { get; set; }
    public ValidationRules Validation { get; set; } = new();
    public List<string> Examples { get; set; } = new();
}

public class FunctionRequestBody
{
    public Dictionary<string, object> Schema { get; set; } = new();
    public string? TemplateRef { get; set; }
    public string ContentType { get; set; } = "application/json";
    public bool Required { get; set; }
}

public class FunctionResponse
{
    public string ExpectedFormat { get; set; } = string.Empty;
    public Dictionary<string, object> Schema { get; set; } = new();
    public Dictionary<string, ResponseStatusCode> StatusCodes { get; set; } = new();
    public List<HeaderDefinition> Headers { get; set; } = new();
}

public class ResponseStatusCode
{
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object>? Schema { get; set; }
}

// ==================== METADATA ====================

public class MetadataInfo
{
    public string? Documentation { get; set; }
    public string? SupportContact { get; set; }
    public List<string> Dependencies { get; set; } = new();
    public Dictionary<string, object> Compatibility { get; set; } = new();
    public bool BetaFeature { get; set; }
    public string? ChangeLog { get; set; }
    public List<string> KnownIssues { get; set; } = new();
}