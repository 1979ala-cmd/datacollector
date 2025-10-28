using System.Text.Json;
using DataCollector.Application.DTOs;
using DataCollector.Application.Interfaces;
using DataCollector.Domain.Entities;
using DataCollector.Domain.Enums;
using DataCollector.Domain.Exceptions;
using DataCollector.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace DataCollector.Application.Services;

public class DataSourceService : IDataSourceService
{
    private readonly IDbContextFactory<TenantDbContext> _contextFactory;

    public DataSourceService(IDbContextFactory<TenantDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<DataSourceDto> CreateAsync(Guid tenantId, CreateDataSourceRequest request, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        // Parse protocol
        if (!Enum.TryParse<DataSourceProtocol>(request.Protocol, true, out var protocol))
            throw new DomainException($"Invalid protocol: {request.Protocol}");

        // Parse type
        if (!Enum.TryParse<DataSourceType>(request.Type, true, out var type))
            throw new DomainException($"Invalid type: {request.Type}");

        // Parse auth type
        if (!Enum.TryParse<AuthType>(request.AuthType, true, out var authType))
            throw new DomainException($"Invalid auth type: {request.AuthType}");

        var dataSource = new DataSource
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name,
            Description = request.Description,
            Protocol = protocol,
            Type = type,
            Endpoint = request.Endpoint,
            AuthType = authType,
            AuthConfig = request.AuthConfig != null ? JsonSerializer.Serialize(request.AuthConfig) : null,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "System"
        };

        context.DataSources.Add(dataSource);
        await context.SaveChangesAsync(cancellationToken);

        return new DataSourceDto(
            dataSource.Id,
            dataSource.Name,
            dataSource.Description,
            dataSource.Protocol,
            dataSource.Endpoint,
            dataSource.IsActive
        );
    }

    public async Task<DataSourceDto> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var dataSource = await context.DataSources
            .FirstOrDefaultAsync(ds => ds.Id == id && ds.TenantId == tenantId && !ds.IsDeleted, cancellationToken);

        if (dataSource == null)
            throw new NotFoundException(nameof(DataSource), id);

        return new DataSourceDto(
            dataSource.Id,
            dataSource.Name,
            dataSource.Description,
            dataSource.Protocol,
            dataSource.Endpoint,
            dataSource.IsActive
        );
    }

    public async Task<IEnumerable<DataSourceDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var dataSources = await context.DataSources
            .Where(ds => ds.TenantId == tenantId && !ds.IsDeleted)
            .OrderBy(ds => ds.Name)
            .ToListAsync(cancellationToken);

        return dataSources.Select(ds => new DataSourceDto(
            ds.Id,
            ds.Name,
            ds.Description,
            ds.Protocol,
            ds.Endpoint,
            ds.IsActive
        ));
    }

    public async Task<DataSourceDto> UpdateAsync(Guid tenantId, Guid id, CreateDataSourceRequest request, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var dataSource = await context.DataSources
            .FirstOrDefaultAsync(ds => ds.Id == id && ds.TenantId == tenantId && !ds.IsDeleted, cancellationToken);

        if (dataSource == null)
            throw new NotFoundException(nameof(DataSource), id);

        dataSource.Name = request.Name;
        dataSource.Description = request.Description;
        dataSource.Endpoint = request.Endpoint;
        dataSource.AuthConfig = request.AuthConfig != null ? JsonSerializer.Serialize(request.AuthConfig) : null;
        dataSource.UpdatedAt = DateTime.UtcNow;
        dataSource.UpdatedBy = "System";

        await context.SaveChangesAsync(cancellationToken);

        return new DataSourceDto(
            dataSource.Id,
            dataSource.Name,
            dataSource.Description,
            dataSource.Protocol,
            dataSource.Endpoint,
            dataSource.IsActive
        );
    }

    public async Task<bool> DeleteAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var dataSource = await context.DataSources
            .FirstOrDefaultAsync(ds => ds.Id == id && ds.TenantId == tenantId && !ds.IsDeleted, cancellationToken);

        if (dataSource == null)
            return false;

        dataSource.IsDeleted = true;
        dataSource.DeletedAt = DateTime.UtcNow;
        dataSource.DeletedBy = "System";

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> TestConnectionAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var dataSource = await context.DataSources
            .FirstOrDefaultAsync(ds => ds.Id == id && ds.TenantId == tenantId && !ds.IsDeleted, cancellationToken);

        if (dataSource == null)
            throw new NotFoundException(nameof(DataSource), id);

        try
        {
            // TODO: Implement actual connection testing based on protocol type
            // For now, just update last tested timestamp
            dataSource.LastTestedAt = DateTime.UtcNow;
            dataSource.LastTestResult = true;

            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch
        {
            dataSource.LastTestedAt = DateTime.UtcNow;
            dataSource.LastTestResult = false;
            await context.SaveChangesAsync(cancellationToken);
            return false;
        }
    }
}
