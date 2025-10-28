using DataCollector.Domain.Enums;
namespace DataCollector.Domain.Entities;

/// <summary>
/// Pipeline entity that references a DataSource function for execution
/// The function defines the API endpoint, method, parameters, etc.
/// </summary>
public class Pipeline : TenantEntity
{
    // Basic Information
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    // Parent References
    public Guid DataCollectorId { get; set; }
    public Guid DataSourceId { get; set; }
    
    // Function Reference - CRITICAL
    // This references a function ID from the DataSource.Functions JSON
    public string FunctionId { get; set; } = string.Empty;
    
    // Function Override (Optional)
    // If provided, these override the function defaults from DataSource
    public string? FunctionName { get; set; } // Display name override
    public string? ApiPath { get; set; } // Path override (if needed)
    public string? Method { get; set; } // Method override (if needed)
    
    // Pipeline-specific Configuration
    public bool IsEnabled { get; set; } = true;
    
    // Parameter Mappings (JSON)
    // Maps data from previous steps to function parameters
    // Example: { "customerId": "$.previousStep.data.id" }
    public string? ParameterMappings { get; set; } // JSON: Dictionary<string, string>
    
    // Static Parameter Values (JSON)
    // Hardcoded parameter values for this pipeline
    // Example: { "status": "active", "limit": 100 }
    public string? StaticParameters { get; set; } // JSON: Dictionary<string, object>
    
    // Data Ingestion Strategy (JSON)
    // Defines how data should be ingested (full-sync, incremental, on-demand)
    public string? DataIngestion { get; set; } // JSON: DataIngestionConfiguration
    
    // Navigation Properties
    public virtual DataCollectorEntity? DataCollector { get; set; }
    public virtual DataSource? DataSource { get; set; }
    public virtual ICollection<ProcessingStep> ProcessingSteps { get; set; } = new List<ProcessingStep>();
}

// Supporting Classes for JSON Serialization

public class DataIngestionConfiguration
{
    public string Strategy { get; set; } = "on-demand"; // full-sync, incremental, on-demand
    public string? SyncMode { get; set; } // full, incremental
    public int BatchSize { get; set; } = 100;
    public bool Parallelization { get; set; }
    public IncrementalConfiguration? IncrementalConfig { get; set; }
    public string ConflictResolution { get; set; } = "latest"; // latest, oldest, merge
}

public class IncrementalConfiguration
{
    public bool Enabled { get; set; }
    public string DeltaStrategy { get; set; } = "both"; // created, updated, both, deleted
    public string TrackingField { get; set; } = "updated_at";
    public string TrackingFieldType { get; set; } = "timestamp"; // timestamp, version, cursor
    public int LookbackWindow { get; set; } = 5; // In minutes
    public string? LastSyncValue { get; set; } // Stores last synced value
}

public class ParameterMapping
{
    public string Source { get; set; } = string.Empty; // JSONPath expression
    public string Target { get; set; } = string.Empty; // Parameter name
    public string TargetLocation { get; set; } = "query"; // query, path, header, body
    public string? DefaultValue { get; set; }
    public bool Required { get; set; }
}
