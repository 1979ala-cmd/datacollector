using DataCollector.Domain.Enums;
namespace DataCollector.Application.DTOs;
public record CollectorDto(Guid Id, string Name, string Description, CollectorStage Stage, bool IsActive, DateTime CreatedAt);
public record CreateCollectorRequest(string Name, string Description, List<CreatePipelineRequest> Pipelines);
public record PipelineDto(Guid Id, string Name, Guid DataSourceId, string ApiPath, string Method, bool IsEnabled);
public record CreatePipelineRequest(string Name, Guid DataSourceId, string ApiPath, string Method, List<object> ProcessingSteps);
