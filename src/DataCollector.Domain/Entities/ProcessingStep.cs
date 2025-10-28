using DataCollector.Domain.Enums;
namespace DataCollector.Domain.Entities;
public class ProcessingStep : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public ProcessingStepType Type { get; set; }
    public Guid PipelineId { get; set; }
    public Guid? ParentStepId { get; set; }
    public int Order { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? Config { get; set; }
    public virtual Pipeline? Pipeline { get; set; }
    public virtual ProcessingStep? ParentStep { get; set; }
    public virtual ICollection<ProcessingStep> ChildSteps { get; set; } = new List<ProcessingStep>();
}
