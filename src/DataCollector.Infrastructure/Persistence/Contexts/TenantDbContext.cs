using Microsoft.EntityFrameworkCore;
using DataCollector.Domain.Entities;
namespace DataCollector.Infrastructure.Persistence.Contexts;
public class TenantDbContext : DbContext
{
    public TenantDbContext(DbContextOptions<TenantDbContext> options) : base(options) { }
    public DbSet<DataSource> DataSources => Set<DataSource>();
    public DbSet<DataCollectorEntity> DataCollectors => Set<DataCollectorEntity>();
    public DbSet<Pipeline> Pipelines => Set<Pipeline>();
    public DbSet<ProcessingStep> ProcessingSteps => Set<ProcessingStep>();
    public DbSet<ApprovalTemplate> ApprovalTemplates => Set<ApprovalTemplate>();
    public DbSet<ApprovalWorkflow> ApprovalWorkflows => Set<ApprovalWorkflow>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<DataSource>(e => {
            e.HasKey(x => x.Id);
            e.HasQueryFilter(x => !x.IsDeleted);
        });
        modelBuilder.Entity<DataCollectorEntity>(e => {
            e.HasKey(x => x.Id);
            e.HasQueryFilter(x => !x.IsDeleted);
        });
    }
}
