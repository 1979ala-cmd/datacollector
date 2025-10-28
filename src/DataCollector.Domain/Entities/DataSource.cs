using DataCollector.Domain.Enums;
namespace DataCollector.Domain.Entities;
public class DataSource : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string? ImageUrl { get; set; }
    public DataSourceProtocol Protocol { get; set; }
    public DataSourceType Type { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public AuthType AuthType { get; set; }
    public string? AuthConfig { get; set; }
    public string? Config { get; set; }
    public string? Headers { get; set; }
    public string? Functions { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Tags { get; set; }
    public string? Metadata { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastTestedAt { get; set; }
    public bool? LastTestResult { get; set; }
    public virtual ICollection<Pipeline> Pipelines { get; set; } = new List<Pipeline>();
}
