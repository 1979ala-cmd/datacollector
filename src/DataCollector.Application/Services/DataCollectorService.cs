using System;
using System.Text.Json;
using DataCollector.Application.DTOs;
using DataCollector.Application.Interfaces;
using DataCollector.Domain.Entities;
using DataCollector.Domain.Enums;
using DataCollector.Domain.Exceptions;
using DataCollector.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace DataCollector.Application.Services;

public class DataCollectorService : IDataCollectorService
{
    private readonly IDbContextFactory<TenantDbContext> _contextFactory;

    public DataCollectorService(IDbContextFactory<TenantDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<CollectorDto> CreateAsync(
        Guid tenantId, 
        CreateCollectorRequest request, 
        string createdBy, 
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        // Validate that all pipelines reference the same data source
        if (request.Pipelines.Any())
        {
            var dataSourceIds = request.Pipelines.Select(p => p.DataSourceId).Distinct().ToList();
            if (dataSourceIds.Count > 1)
                throw new DomainException("All pipelines in a collector must use the same data source");

            // Verify data source exists
            var dataSourceId = dataSourceIds.First();
            var dataSourceExists = await context.DataSources
                .AnyAsync(ds => ds.Id == dataSourceId && ds.TenantId == tenantId && !ds.IsDeleted, cancellationToken);

            if (!dataSourceExists)
                throw new NotFoundException(nameof(DataSource), dataSourceId);
        }

        var collector = new DataCollectorEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name,
            Description = request.Description,
            Version = 1,
            Stage = CollectorStage.Draft,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };

        // Create pipelines
        foreach (var pipelineRequest in request.Pipelines)
        {
            var pipeline = new Pipeline
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                DataCollectorId = collector.Id,
                DataSourceId = pipelineRequest.DataSourceId,
                Name = pipelineRequest.Name,
                ApiPath = pipelineRequest.ApiPath,
                Method = pipelineRequest.Method,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = createdBy
            };

            // Create processing steps
            var order = 0;
            foreach (var stepRequest in pipelineRequest.ProcessingSteps)
            {
                CreateProcessingStep(stepRequest, pipeline.Id, tenantId, null, ref order, createdBy, pipeline.ProcessingSteps);
            }

            collector.Pipelines.Add(pipeline);
        }

        context.DataCollectors.Add(collector);
        await context.SaveChangesAsync(cancellationToken);

        return new CollectorDto(
            collector.Id,
            collector.Name,
            collector.Description,
            collector.Stage,
            collector.IsActive,
            collector.CreatedAt
        );
    }

    public async Task<CollectorDto> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var collector = await context.DataCollectors
            .Include(c => c.Pipelines)
                .ThenInclude(p => p.ProcessingSteps.Where(s => s.ParentStepId == null))
                    .ThenInclude(s => s.ChildSteps)
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId && !c.IsDeleted, cancellationToken);

        if (collector == null)
            throw new NotFoundException(nameof(DataCollectorEntity), id);

        return new CollectorDto(
            collector.Id,
            collector.Name,
            collector.Description,
            collector.Stage,
            collector.IsActive,
            collector.CreatedAt
        );
    }

    public async Task<IEnumerable<CollectorDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var collectors = await context.DataCollectors
            .Where(c => c.TenantId == tenantId && !c.IsDeleted)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        return collectors.Select(c => new CollectorDto(
            c.Id,
            c.Name,
            c.Description,
            c.Stage,
            c.IsActive,
            c.CreatedAt
        ));
    }

    public async Task<CollectorDto> UpdateAsync(
        Guid tenantId, 
        Guid id, 
        CreateCollectorRequest request, 
        string updatedBy, 
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var collector = await context.DataCollectors
            .Include(c => c.Pipelines)
                .ThenInclude(p => p.ProcessingSteps)
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId && !c.IsDeleted, cancellationToken);

        if (collector == null)
            throw new NotFoundException(nameof(DataCollectorEntity), id);

        if (collector.Stage == CollectorStage.Production)
            throw new DomainException("Cannot update collector in Production stage. Create a new version instead.");

        collector.Name = request.Name;
        collector.Description = request.Description;
        collector.UpdatedAt = DateTime.UtcNow;
        collector.UpdatedBy = updatedBy;

        // Remove existing pipelines and steps
        context.ProcessingSteps.RemoveRange(collector.Pipelines.SelectMany(p => p.ProcessingSteps));
        context.Pipelines.RemoveRange(collector.Pipelines);

        // Create new pipelines
        collector.Pipelines.Clear();
        foreach (var pipelineRequest in request.Pipelines)
        {
            var pipeline = new Pipeline
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                DataCollectorId = collector.Id,
                DataSourceId = pipelineRequest.DataSourceId,
                Name = pipelineRequest.Name,
                ApiPath = pipelineRequest.ApiPath,
                Method = pipelineRequest.Method,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = updatedBy
            };

            var order = 0;
            foreach (var stepRequest in pipelineRequest.ProcessingSteps)
            {
                CreateProcessingStep(stepRequest, pipeline.Id, tenantId, null, ref order, updatedBy, pipeline.ProcessingSteps);
            }

            collector.Pipelines.Add(pipeline);
        }

        await context.SaveChangesAsync(cancellationToken);

        return new CollectorDto(
            collector.Id,
            collector.Name,
            collector.Description,
            collector.Stage,
            collector.IsActive,
            collector.CreatedAt
        );
    }

    public async Task<bool> DeleteAsync(Guid tenantId, Guid id, string deletedBy, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var collector = await context.DataCollectors
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId && !c.IsDeleted, cancellationToken);

        if (collector == null)
            return false;

        if (collector.Stage == CollectorStage.Production)
            throw new DomainException("Cannot delete collector in Production stage. Archive it instead.");

        collector.IsDeleted = true;
        collector.DeletedAt = DateTime.UtcNow;
        collector.DeletedBy = deletedBy;

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<CollectorExecutionResult> ExecuteAsync(
        Guid tenantId, 
        Guid collectorId, 
        Guid pipelineId, 
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var collector = await context.DataCollectors
            .Include(c => c.Pipelines.Where(p => p.Id == pipelineId))
                .ThenInclude(p => p.ProcessingSteps.Where(s => s.ParentStepId == null))
                    .ThenInclude(s => s.ChildSteps)
            .Include(c => c.Pipelines)
                .ThenInclude(p => p.DataSource)
            .FirstOrDefaultAsync(c => c.Id == collectorId && c.TenantId == tenantId && !c.IsDeleted, cancellationToken);

        if (collector == null)
            throw new NotFoundException(nameof(DataCollectorEntity), collectorId);

        var pipeline = collector.Pipelines.FirstOrDefault(p => p.Id == pipelineId);
        if (pipeline == null)
            throw new NotFoundException(nameof(Pipeline), pipelineId);

        if (!pipeline.IsEnabled)
            throw new DomainException("Pipeline is not enabled");

        var executionId = Guid.NewGuid();
        var startedAt = DateTime.UtcNow;

        try
        {
            // Execute pipeline steps
            var result = await ExecutePipelineAsync(pipeline, cancellationToken);

            return new CollectorExecutionResult(
                executionId,
                collectorId,
                pipelineId,
                true,
                "Pipeline executed successfully",
                result,
                startedAt,
                DateTime.UtcNow,
                result?.RecordsProcessed ?? 0
            );
        }
        catch (Exception ex)
        {
            return new CollectorExecutionResult(
                executionId,
                collectorId,
                pipelineId,
                false,
                $"Pipeline execution failed: {ex.Message}",
                null,
                startedAt,
                DateTime.UtcNow,
                0
            );
        }
    }

    private void CreateProcessingStep(
        dynamic stepRequest,
        Guid pipelineId,
        Guid tenantId,
        Guid? parentStepId,
        ref int order,
        string createdBy,
        ICollection<ProcessingStep> collection)
    {
        var step = new ProcessingStep
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PipelineId = pipelineId,
            ParentStepId = parentStepId,
            Name = stepRequest.Name?.ToString() ?? "Unnamed Step",
            Type = ParseStepType(stepRequest.Type?.ToString() ?? "ApiCall"),
            Order = order++,
            IsEnabled = stepRequest.Enabled ?? true,
            Config = stepRequest.Config != null ? JsonSerializer.Serialize(stepRequest.Config) : null,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };

        // Handle child steps recursively
        if (stepRequest.ChildSteps != null)
        {
            var childOrder = 0;
            foreach (var childRequest in stepRequest.ChildSteps)
            {
                CreateProcessingStep(childRequest, pipelineId, tenantId, step.Id, ref childOrder, createdBy, step.ChildSteps);
            }
        }

        collection.Add(step);
    }

    private ProcessingStepType ParseStepType(string type)
    {
        return type?.ToLower() switch
        {
            "api-call" => ProcessingStepType.ApiCall,
            "pagination" => ProcessingStepType.Pagination,
            "retry" => ProcessingStepType.Retry,
            "filter" => ProcessingStepType.Filter,
            "for-each" => ProcessingStepType.ForEach,
            "transform" => ProcessingStepType.Transform,
            "field-selector" => ProcessingStepType.FieldSelector,
            "store-database" => ProcessingStepType.StoreDatabase,
            "store-disk" => ProcessingStepType.StoreDisk,
            _ => ProcessingStepType.ApiCall
        };
    }

    private async Task<dynamic?> ExecutePipelineAsync(Pipeline pipeline, CancellationToken cancellationToken)
    {
        // Get root-level steps (no parent)
        var rootSteps = pipeline.ProcessingSteps
            .Where(s => s.ParentStepId == null && s.IsEnabled)
            .OrderBy(s => s.Order)
            .ToList();

        object? currentData = null;
        var recordsProcessed = 0;

        foreach (var step in rootSteps)
        {
            currentData = await ExecuteStepAsync(step, currentData, pipeline, cancellationToken);
            recordsProcessed++;
        }

        return new { Success = true, RecordsProcessed = recordsProcessed, Data = currentData };
    }

    private async Task<object?> ExecuteStepAsync(
        ProcessingStep step, 
        object? inputData, 
        Pipeline pipeline, 
        CancellationToken cancellationToken)
    {
        if (!step.IsEnabled)
            return inputData;

        object? result = step.Type switch
        {
            ProcessingStepType.ApiCall => await ExecuteApiCallAsync(step, pipeline, cancellationToken),
            ProcessingStepType.Pagination => await ExecutePaginationAsync(step, inputData, pipeline, cancellationToken),
            ProcessingStepType.Retry => await ExecuteRetryAsync(step, inputData, pipeline, cancellationToken),
            ProcessingStepType.Filter => ExecuteFilter(step, inputData),
            ProcessingStepType.ForEach => await ExecuteForEachAsync(step, inputData, pipeline, cancellationToken),
            ProcessingStepType.Transform => ExecuteTransform(step, inputData),
            ProcessingStepType.FieldSelector => ExecuteFieldSelector(step, inputData),
            ProcessingStepType.StoreDatabase => await ExecuteStoreDatabaseAsync(step, inputData, cancellationToken),
            ProcessingStepType.StoreDisk => await ExecuteStoreDiskAsync(step, inputData, cancellationToken),
            _ => inputData
        };

        // Execute child steps if any
        if (step.ChildSteps.Any())
        {
            foreach (var childStep in step.ChildSteps.Where(s => s.IsEnabled).OrderBy(s => s.Order))
            {
                result = await ExecuteStepAsync(childStep, result, pipeline, cancellationToken);
            }
        }

        return result;
    }

    private async Task<object?> ExecuteApiCallAsync(ProcessingStep step, Pipeline pipeline, CancellationToken cancellationToken)
    {
        // TODO: Implement actual HTTP call to the API endpoint
        // For now, return mock data
        await Task.Delay(100, cancellationToken);
        return new { Status = "Success", Message = $"API call to {pipeline.ApiPath} executed", Timestamp = DateTime.UtcNow };
    }

    private async Task<object?> ExecutePaginationAsync(ProcessingStep step, object? inputData, Pipeline pipeline, CancellationToken cancellationToken)
    {
        // TODO: Implement pagination logic
        await Task.Delay(50, cancellationToken);
        return inputData;
    }

    private async Task<object?> ExecuteRetryAsync(ProcessingStep step, object? inputData, Pipeline pipeline, CancellationToken cancellationToken)
    {
        // TODO: Implement retry logic with exponential backoff
        await Task.Delay(50, cancellationToken);
        return inputData;
    }

    private object? ExecuteFilter(ProcessingStep step, object? inputData)
    {
        // TODO: Implement filtering logic based on step config
        return inputData;
    }

    private async Task<object?> ExecuteForEachAsync(ProcessingStep step, object? inputData, Pipeline pipeline, CancellationToken cancellationToken)
    {
        // TODO: Implement for-each iteration logic
        await Task.Delay(50, cancellationToken);
        return inputData;
    }

    private object? ExecuteTransform(ProcessingStep step, object? inputData)
    {
        // TODO: Implement transformation logic based on step config
        return inputData;
    }

    private object? ExecuteFieldSelector(ProcessingStep step, object? inputData)
    {
        // TODO: Implement field selection logic
        return inputData;
    }

    private async Task<object?> ExecuteStoreDatabaseAsync(ProcessingStep step, object? inputData, CancellationToken cancellationToken)
    {
        // TODO: Implement database storage logic
        await Task.Delay(50, cancellationToken);
        return inputData;
    }

    private async Task<object?> ExecuteStoreDiskAsync(ProcessingStep step, object? inputData, CancellationToken cancellationToken)
    {
        // TODO: Implement disk storage logic
        await Task.Delay(50, cancellationToken);
        return inputData;
    }
}
