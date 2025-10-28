using DataCollector.Domain.Enums;
namespace DataCollector.Domain.Entities;

/// <summary>
/// DataCollector - Created by a user, can be published for other tenants to use
/// Stores creator's config for testing, but tenants provide their own config when installing
/// </summary>
public class DataCollectorEntity : TenantEntity
{
    // Basic Information
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public CollectorStage Stage { get; set; } = CollectorStage.Draft;
    public bool IsActive { get; set; } = true;
    
    // DataSource Reference (Template)
    public Guid DataSourceId { get; set; }
    
    // Publishing
    public bool IsPublished { get; set; } = false;
    public DateTime? PublishedAt { get; set; }
    public string? PublishedBy { get; set; }
    
    // Creator's Configuration (for testing only)
    // This is the creator's own settings (their shopUrl, apiKey, etc.)
    // NOT used when tenants execute - only for creator's testing
    public string? CreatorConfig { get; set; } // JSON: Dictionary<string, object>
    
    // Promotion History
    public DateTime? PromotedAt { get; set; }
    public string? PromotedBy { get; set; }
    public ApprovalStatus? ApprovalStatus { get; set; }
    
    // Navigation Properties
    public virtual DataSource? DataSource { get; set; }
    public virtual ICollection<Pipeline> Pipelines { get; set; } = new List<Pipeline>();
    public virtual ICollection<ApprovalWorkflow> ApprovalWorkflows { get; set; } = new List<ApprovalWorkflow>();
    public virtual ICollection<CollectorInstance> CollectorInstances { get; set; } = new List<CollectorInstance>();
}

/// <summary>
/// CollectorInstance - Tenant's installation of a published Collector
/// Stores TENANT'S OWN configuration for the DataSource
/// </summary>
public class CollectorInstance : TenantEntity
{
    // References
    public Guid CollectorId { get; set; }
    public Guid InstalledByTenantId { get; set; } // The tenant who installed this
    
    // Tenant's Configuration
    // This is where the tenant's OWN settings are stored
    // (their shopUrl, their apiKey, their specific config values)
    public string TenantConfig { get; set; } = string.Empty; // JSON: Dictionary<string, object>
    
    // Instance Information
    public string? InstanceName { get; set; } // Tenant can give it a custom name
    public bool IsActive { get; set; } = true;
    
    // Execution History
    public DateTime? LastExecutedAt { get; set; }
    public DateTime? LastSuccessfulExecutionAt { get; set; }
    public int TotalExecutions { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }
    
    // Scheduling (if tenant wants automated runs)
    public bool ScheduleEnabled { get; set; } = false;
    public string? ScheduleConfig { get; set; } // JSON: ScheduleConfiguration
    
    // Navigation Properties
    public virtual DataCollectorEntity? Collector { get; set; }
    public virtual ICollection<CollectorExecution> Executions { get; set; } = new List<CollectorExecution>();
}

/// <summary>
/// CollectorExecution - Record of each execution of a CollectorInstance
/// </summary>
public class CollectorExecution : BaseEntity
{
    public Guid CollectorInstanceId { get; set; }
    public Guid PipelineId { get; set; }
    
    // Execution Details
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "Running"; // Running, Success, Failed
    
    // Results
    public int RecordsProcessed { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ExecutionLog { get; set; } // JSON: Detailed log
    
    // Performance
    public int DurationMs { get; set; }
    
    // Navigation Properties
    public virtual CollectorInstance? CollectorInstance { get; set; }
}

/// <summary>
/// Supporting configuration classes
/// </summary>
public class ScheduleConfiguration
{
    public string Frequency { get; set; } = "daily"; // daily, weekly, monthly, custom
    public string? CronExpression { get; set; }
    public string TimeZone { get; set; } = "UTC";
    public DateTime? NextRunAt { get; set; }
}