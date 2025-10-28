using DataCollector.Application.DTOs;

namespace DataCollector.Application.Interfaces;

public interface ITenantService
{
    Task<TenantCreatedResponse> CreateTenantAsync(CreateTenantRequest request, CancellationToken cancellationToken = default);
    Task<TenantDto> GetTenantAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TenantDto> GetTenantBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<IEnumerable<TenantDto>> GetAllTenantsAsync(CancellationToken cancellationToken = default);
    Task<bool> ProvisionTenantDatabaseAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
