namespace DataCollector.Domain.Entities;
public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public Guid TenantId { get; set; }
    public virtual Tenant? Tenant { get; set; }
    public virtual ICollection<UserRoleEntity> UserRoles { get; set; } = new List<UserRoleEntity>();
    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}

public class UserRoleEntity : BaseEntity
{
    public Guid UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public virtual User? User { get; set; }
}
