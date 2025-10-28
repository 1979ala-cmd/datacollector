namespace DataCollector.Domain.Entities;
public class Pipeline : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid DataCollectorId { get; set; }
    public Guid DataSourceId { get; set; }
    public string ApiPath { get; set; } = string.Empty;
    public string Method { get; set; } = "GET";
    public bool IsEnabled { get; set; } = true;
    public string? Parameters { get; set; }
    public string? DataIngestion { get; set; }
    public virtual DataCollectorEntity? DataCollector { get; set; }
    public virtual DataSource? DataSource { get; set; }
    public virtual ICollection<ProcessingStep> ProcessingSteps { get; set; } = new List<ProcessingStep>();
}
