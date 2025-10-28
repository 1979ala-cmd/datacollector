using DataCollector.Application.DTOs;

namespace DataCollector.Application.Interfaces;

public interface IDataSourceService
{
    Task<DataSourceDto> CreateAsync(Guid tenantId, CreateDataSourceRequest request, CancellationToken cancellationToken = default);
    Task<DataSourceDto> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<DataSourceDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<DataSourceDto> UpdateAsync(Guid tenantId, Guid id, CreateDataSourceRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
}
