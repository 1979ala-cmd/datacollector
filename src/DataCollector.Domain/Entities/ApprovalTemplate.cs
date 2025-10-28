namespace DataCollector.Domain.Entities;
public class ApprovalTemplate : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RequiredChecks { get; set; } = string.Empty;
    public bool RequiresSecurityReview { get; set; }
    public bool RequiresTestCoverage { get; set; }
    public int? MinTestCoveragePercent { get; set; }
    public bool IsActive { get; set; } = true;
}
