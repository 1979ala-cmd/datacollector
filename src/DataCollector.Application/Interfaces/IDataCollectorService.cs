using DataCollector.Application.DTOs;

namespace DataCollector.Application.Interfaces;

public interface IDataCollectorService
{
    Task<CollectorDto> CreateAsync(Guid tenantId, CreateCollectorRequest request, string createdBy, CancellationToken cancellationToken = default);
    Task<CollectorDto> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<CollectorDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<CollectorDto> UpdateAsync(Guid tenantId, Guid id, CreateCollectorRequest request, string updatedBy, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid tenantId, Guid id, string deletedBy, CancellationToken cancellationToken = default);
    Task<CollectorExecutionResult> ExecuteAsync(Guid tenantId, Guid collectorId, Guid pipelineId, CancellationToken cancellationToken = default);
}
