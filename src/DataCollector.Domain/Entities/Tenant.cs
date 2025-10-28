namespace DataCollector.Domain.Entities;
public class Tenant : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string? ConnectionString { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Settings { get; set; }
    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
