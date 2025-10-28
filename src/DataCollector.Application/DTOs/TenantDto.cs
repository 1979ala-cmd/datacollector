namespace DataCollector.Application.DTOs;
public record TenantDto(Guid Id, string Name, string Slug, string DatabaseName, bool IsActive, DateTime CreatedAt);
public record CreateTenantRequest(string Name, string AdminEmail, string AdminPassword, string? Slug = null);
public record TenantCreatedResponse(Guid TenantId, string Name, string Slug, string DatabaseName, string Status, Guid AdminUserId, DateTime CreatedAt);
