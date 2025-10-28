using DataCollector.Domain.Enums;
namespace DataCollector.Domain.Entities;
public class ApprovalWorkflow : TenantEntity
{
    public Guid DataCollectorId { get; set; }
    public CollectorStage FromStage { get; set; }
    public CollectorStage ToStage { get; set; }
    public ApprovalStatus Status { get; set; }
    public string? RequestedBy { get; set; }
    public DateTime RequestedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? Comment { get; set; }
    public virtual DataCollectorEntity? DataCollector { get; set; }
}
