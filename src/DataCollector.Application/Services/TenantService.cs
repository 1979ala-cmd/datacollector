using DataCollector.Application.DTOs;
using DataCollector.Application.Interfaces;
using DataCollector.Domain.Entities;
using DataCollector.Domain.Enums;
using DataCollector.Domain.Exceptions;
using DataCollector.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace DataCollector.Application.Services;

public class TenantService : ITenantService
{
    private readonly SharedDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IConfiguration _configuration;

    public TenantService(
        SharedDbContext context,
        IPasswordHasher passwordHasher,
        IConfiguration configuration)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
    }

    public async Task<TenantCreatedResponse> CreateTenantAsync(CreateTenantRequest request, CancellationToken cancellationToken = default)
    {
        // Generate slug if not provided
        var slug = request.Slug ?? GenerateSlug(request.Name);

        // Check if tenant with slug already exists
        var existingTenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.Slug == slug && !t.IsDeleted, cancellationToken);

        if (existingTenant != null)
            throw new DomainException($"Tenant with slug '{slug}' already exists");

        // Check if admin email already exists
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.AdminEmail && !u.IsDeleted, cancellationToken);

        if (existingUser != null)
            throw new DomainException($"User with email '{request.AdminEmail}' already exists");

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Slug = slug,
            DatabaseName = $"{_configuration["MultiTenancy:DatabasePrefix"]}{slug}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "System"
        };

        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Email = request.AdminEmail,
            PasswordHash = _passwordHasher.HashPassword(request.AdminPassword),
            FirstName = "Admin",
            LastName = "User",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "System"
        };

        var adminRole = new UserRoleEntity
        {
            Id = Guid.NewGuid(),
            UserId = adminUser.Id,
            Role = UserRole.Admin.ToString(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "System"
        };

        _context.Tenants.Add(tenant);
        _context.Users.Add(adminUser);
        _context.UserRoles.Add(adminRole);

        await _context.SaveChangesAsync(cancellationToken);

        // Provision tenant database
        var autoCreate = bool.Parse(_configuration["MultiTenancy:AutoCreateDatabase"] ?? "true");
        if (autoCreate)
        {
            await ProvisionTenantDatabaseAsync(tenant.Id, cancellationToken);
        }

        return new TenantCreatedResponse(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.DatabaseName,
            "Active",
            adminUser.Id,
            tenant.CreatedAt
        );
    }

    public async Task<TenantDto> GetTenantAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, cancellationToken);

        if (tenant == null)
            throw new NotFoundException(nameof(Tenant), id);

        return new TenantDto(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.DatabaseName,
            tenant.IsActive,
            tenant.CreatedAt
        );
    }

    public async Task<TenantDto> GetTenantBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.Slug == slug && !t.IsDeleted, cancellationToken);

        if (tenant == null)
            throw new NotFoundException(nameof(Tenant), slug);

        return new TenantDto(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.DatabaseName,
            tenant.IsActive,
            tenant.CreatedAt
        );
    }

    public async Task<IEnumerable<TenantDto>> GetAllTenantsAsync(CancellationToken cancellationToken = default)
    {
        var tenants = await _context.Tenants
            .Where(t => !t.IsDeleted)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        return tenants.Select(t => new TenantDto(
            t.Id,
            t.Name,
            t.Slug,
            t.DatabaseName,
            t.IsActive,
            t.CreatedAt
        ));
    }

    public async Task<bool> ProvisionTenantDatabaseAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId && !t.IsDeleted, cancellationToken);

        if (tenant == null)
            throw new NotFoundException(nameof(Tenant), tenantId);

        try
        {
            var sharedConnectionString = _configuration.GetConnectionString("SharedDatabase");
            var builder = new NpgsqlConnectionStringBuilder(sharedConnectionString);
            
            // Connect to postgres database to create new database
            var masterDbName = builder.Database;
            builder.Database = "postgres";

            await using var masterConnection = new NpgsqlConnection(builder.ConnectionString);
            await masterConnection.OpenAsync(cancellationToken);

            // Check if database exists
            var checkDbCommand = masterConnection.CreateCommand();
            checkDbCommand.CommandText = $"SELECT 1 FROM pg_database WHERE datname = '{tenant.DatabaseName}'";
            var exists = await checkDbCommand.ExecuteScalarAsync(cancellationToken);

            if (exists == null)
            {
                // Create database
                var createDbCommand = masterConnection.CreateCommand();
                createDbCommand.CommandText = $"CREATE DATABASE \"{tenant.DatabaseName}\"";
                await createDbCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            // Build tenant connection string
            builder.Database = tenant.DatabaseName;
            tenant.ConnectionString = builder.ConnectionString;

            // Create tables in tenant database
            var optionsBuilder = new DbContextOptionsBuilder<TenantDbContext>();
            optionsBuilder.UseNpgsql(tenant.ConnectionString);

            await using var tenantContext = new TenantDbContext(optionsBuilder.Options);
            await tenantContext.Database.MigrateAsync(cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            throw new DomainException($"Failed to provision database for tenant: {ex.Message}");
        }
    }

    private static string GenerateSlug(string name)
    {
        return name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("_", "-")
            .Replace(".", "-");
    }
}
