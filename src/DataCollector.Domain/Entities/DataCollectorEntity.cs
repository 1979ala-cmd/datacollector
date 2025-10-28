using DataCollector.Domain.Enums;
namespace DataCollector.Domain.Entities;
public class DataCollectorEntity : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public CollectorStage Stage { get; set; } = CollectorStage.Draft;
    public bool IsActive { get; set; } = true;
    public string? Config { get; set; }
    public DateTime? PromotedAt { get; set; }
    public string? PromotedBy { get; set; }
    public ApprovalStatus? ApprovalStatus { get; set; }
    public virtual ICollection<Pipeline> Pipelines { get; set; } = new List<Pipeline>();
    public virtual ICollection<ApprovalWorkflow> ApprovalWorkflows { get; set; } = new List<ApprovalWorkflow>();
}
