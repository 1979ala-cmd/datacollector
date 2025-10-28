namespace DataCollector.Domain.Entities;
public abstract class TenantEntity : BaseEntity
{
    public Guid TenantId { get; set; }
    public virtual Tenant? Tenant { get; set; }
}
