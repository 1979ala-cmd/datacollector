using DataCollector.Domain.Enums;

namespace DataCollector.Domain.DTOs;

public record CollectorExecutionResult(
    Guid ExecutionId,
    Guid CollectorId,
    Guid PipelineId,
    bool Success,
    string Message,
    object? Data,
    DateTime StartedAt,
    DateTime? CompletedAt,
    int RecordsProcessed
);

public record ExecuteCollectorRequest(
    Guid PipelineId,
    Dictionary<string, object>? Parameters = null
);

public record ProcessingStepDto(
    Guid Id,
    string Name,
    ProcessingStepType Type,
    int Order,
    bool IsEnabled,
    string? Config,
    List<ProcessingStepDto>? ChildSteps = null
);
