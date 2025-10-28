using System.Text.Json;
using System.Text;
using System.Net.Http.Headers;
using DataCollector.Application.DTOs;
using DataCollector.Application.Interfaces;
using DataCollector.Application.Services.Parsers;
using DataCollector.Domain.Entities;
using DataCollector.Domain.Exceptions;
using DataCollector.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DataCollector.Application.Services;

/// <summary>
/// Complete DataSource service with integrated Swagger, GraphQL, and WSDL parsers
/// </summary>
public class DataSourceService : IDataSourceService
{
    private readonly IDbContextFactory<TenantDbContext> _contextFactory;
    private readonly ILogger<DataSourceService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SwaggerParser _swaggerParser;
    private readonly GraphQLParser _graphqlParser;
    private readonly WsdlParser _wsdlParser;

    public DataSourceService(
        IDbContextFactory<TenantDbContext> contextFactory,
        ILogger<DataSourceService> logger,
        IHttpClientFactory httpClientFactory,
        SwaggerParser swaggerParser,
        GraphQLParser graphqlParser,
        WsdlParser wsdlParser)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _swaggerParser = swaggerParser;
        _graphqlParser = graphqlParser;
        _wsdlParser = wsdlParser;
    }

    // ==================== MANUAL DATASOURCE CREATION ====================

    public async Task<DataSourceResponseDto> CreateManualAsync(
        Guid tenantId,
        CreateManualDataSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating manual DataSource: {Name} for tenant: {TenantId}", request.Name, tenantId);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        // Check for duplicate name
        var exists = await context.DataSources
            .AnyAsync(ds => ds.TenantId == tenantId && ds.Name == request.Name && !ds.IsDeleted, cancellationToken);

        if (exists)
            throw new DomainException($"DataSource with name '{request.Name}' already exists");

        // Build ConfigRoot from request
        var configRoot = BuildConfigRoot(request);

        // Build Functions from request
        var functions = BuildFunctions(request.Functions);

        // Build Headers
        var headers = request.Headers?.Select(h => new HeaderDefinition
        {
            Name = h.Name,
            Value = h.Value,
            Required = h.Required,
            IsDynamic = h.IsDynamic
        }).ToList();

        // Build Body configuration
        var body = request.Body != null ? new BodyConfiguration
        {
            DefaultFormat = request.Body.DefaultFormat,
            Templates = request.Body.Templates,
            Schemas = request.Body.Schemas
        } : null;

        // Build Metadata
        var metadata = request.Metadata != null ? new MetadataInfo
        {
            Documentation = request.Metadata.Documentation,
            SupportContact = request.Metadata.SupportContact,
            Dependencies = request.Metadata.Dependencies,
            Compatibility = request.Metadata.Compatibility,
            BetaFeature = request.Metadata.BetaFeature,
            ChangeLog = request.Metadata.ChangeLog,
            KnownIssues = request.Metadata.KnownIssues
        } : null;

        // Create DataSource entity
        var dataSource = new DataSource
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name,
            Description = request.Description,
            Version = request.Version,
            ImageUrl = request.ImageUrl,
            Protocol = request.Protocol,
            Config = JsonSerializer.Serialize(configRoot),
            Headers = headers != null ? JsonSerializer.Serialize(headers) : null,
            Body = body != null ? JsonSerializer.Serialize(body) : null,
            Functions = JsonSerializer.Serialize(functions),
            Category = request.Category,
            Tags = request.Tags != null ? JsonSerializer.Serialize(request.Tags) : null,
            Metadata = metadata != null ? JsonSerializer.Serialize(metadata) : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = "System",
            IsActive = true
        };

        context.DataSources.Add(dataSource);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created DataSource: {Id} - {Name}", dataSource.Id, dataSource.Name);

        return MapToDto(dataSource);
    }

    public async Task<DataSourceResponseDto> UpdateAsync(
        Guid tenantId,
        Guid dataSourceId,
        CreateManualDataSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating DataSource: {Id} for tenant: {TenantId}", dataSourceId, tenantId);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var dataSource = await context.DataSources
            .FirstOrDefaultAsync(ds => ds.Id == dataSourceId && ds.TenantId == tenantId && !ds.IsDeleted, cancellationToken);

        if (dataSource == null)
            throw new NotFoundException(nameof(DataSource), dataSourceId);

        // Update fields
        dataSource.Name = request.Name;
        dataSource.Description = request.Description;
        dataSource.Version = request.Version;
        dataSource.ImageUrl = request.ImageUrl;
        dataSource.Protocol = request.Protocol;
        dataSource.Category = request.Category;
        dataSource.UpdatedAt = DateTime.UtcNow;

        // Update configurations
        var configRoot = BuildConfigRoot(request);
        dataSource.Config = JsonSerializer.Serialize(configRoot);

        var functions = BuildFunctions(request.Functions);
        dataSource.Functions = JsonSerializer.Serialize(functions);

        if (request.Headers != null)
        {
            var headers = request.Headers.Select(h => new HeaderDefinition
            {
                Name = h.Name,
                Value = h.Value,
                Required = h.Required,
                IsDynamic = h.IsDynamic
            }).ToList();
            dataSource.Headers = JsonSerializer.Serialize(headers);
        }

        if (request.Body != null)
        {
            var body = new BodyConfiguration
            {
                DefaultFormat = request.Body.DefaultFormat,
                Templates = request.Body.Templates,
                Schemas = request.Body.Schemas
            };
            dataSource.Body = JsonSerializer.Serialize(body);
        }

        if (request.Tags != null)
            dataSource.Tags = JsonSerializer.Serialize(request.Tags);

        if (request.Metadata != null)
        {
            var metadata = new MetadataInfo
            {
                Documentation = request.Metadata.Documentation,
                SupportContact = request.Metadata.SupportContact,
                Dependencies = request.Metadata.Dependencies,
                Compatibility = request.Metadata.Compatibility,
                BetaFeature = request.Metadata.BetaFeature,
                ChangeLog = request.Metadata.ChangeLog,
                KnownIssues = request.Metadata.KnownIssues
            };
            dataSource.Metadata = JsonSerializer.Serialize(metadata);
        }

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated DataSource: {Id}", dataSourceId);

        return MapToDto(dataSource);
    }

    // ==================== DATASOURCE GENERATION WITH PARSERS ====================

    public async Task<DataSourceResponseDto> GenerateFromSwaggerUrlAsync(
        Guid tenantId,
        GenerateDataSourceFromUrlRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating DataSource from Swagger URL: {Url}", request.Source);

        try
        {
            // Fetch Swagger content
            var httpClient = _httpClientFactory.CreateClient();
            var swaggerContent = await httpClient.GetStringAsync(request.Source, cancellationToken);

            // Parse using SwaggerParser
            var parseResult = _swaggerParser.Parse(swaggerContent);

            // Filter operations if needed
            var functions = parseResult.Functions;
            if (request.FilterOperations && request.IncludedOperations != null)
            {
                functions = functions.Where(f => request.IncludedOperations.Contains(f.Name)).ToList();
            }

            // Extract base URL
            var baseUrl = request.BaseUrlOverride ?? parseResult.BaseUrl ?? request.Source;

            // Create auth configuration from security schemes
            var authConfig = CreateAuthFromSecuritySchemes(parseResult.SecuritySchemes, request);

            // Build config root
            var configRoot = new ConfigRoot
            {
                Fields = new List<ConfigField>
                {
                    new ConfigField
                    {
                        Name = "base_url",
                        Type = 10,
                        Label = "Base URL",
                        Description = "API base URL",
                        Required = true,
                        Default = baseUrl,
                        Validation = new ValidationRules()
                    }
                },
                Auth = authConfig,
                RateLimit = new RateLimitConfiguration
                {
                    RequestsPerMinute = request.DefaultRateLimit,
                    RequestsPerHour = request.DefaultRateLimit * 60,
                    RequestsPerDay = request.DefaultRateLimit * 1440,
                    Strategy = "fixed"
                },
                Cache = new CacheConfiguration { Enabled = false },
                Retry = new RetryConfiguration
                {
                    Enabled = true,
                    MaxAttempts = 3,
                    InitialDelayMs = 1000,
                    BackoffStrategy = "exponential",
                    RetryableStatusCodes = new List<int> { 429, 500, 502, 503, 504 }
                },
                Monitoring = new MonitoringConfiguration { Enabled = true },
                CircuitBreaker = new CircuitBreakerConfiguration { Enabled = true }
            };

            // Create DataSource
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var dataSource = new DataSource
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = request.DataSourceName,
                Description = request.Description ?? parseResult.Description ?? "",
                Version = parseResult.Version,
                Protocol = 0, // REST
                Config = JsonSerializer.Serialize(configRoot),
                Functions = JsonSerializer.Serialize(functions),
                Category = "Generated",
                Tags = JsonSerializer.Serialize(new List<string> { "swagger", "openapi", "generated" }),
                Metadata = JsonSerializer.Serialize(new MetadataInfo
                {
                    Documentation = parseResult.Title
                }),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = "Swagger Generator",
                IsActive = true
            };

            context.DataSources.Add(dataSource);
            await context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Generated DataSource from Swagger: {Id} - {Name}", dataSource.Id, dataSource.Name);

            return MapToDto(dataSource);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate DataSource from Swagger URL: {Url}", request.Source);
            throw new DomainException($"Failed to generate DataSource from Swagger: {ex.Message}");
        }
    }

    public async Task<DataSourceResponseDto> GenerateFromSwaggerContentAsync(
        Guid tenantId,
        GenerateDataSourceFromContentRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating DataSource from Swagger content");

        try
        {
            // Parse using SwaggerParser
            var parseResult = _swaggerParser.Parse(request.Source);

            // Filter operations if needed
            var functions = parseResult.Functions;

            // Extract base URL
            var baseUrl = parseResult.BaseUrl ?? "";

            // Create auth configuration from security schemes
            var authConfig = CreateAuthFromSecuritySchemes(parseResult.SecuritySchemes, 
                new GenerateDataSourceFromUrlRequest { OverrideAuth = false });

            // Build config root
            var configRoot = new ConfigRoot
            {
                Fields = new List<ConfigField>
                {
                    new ConfigField
                    {
                        Name = "base_url",
                        Type = 10,
                        Label = "Base URL",
                        Description = "API base URL",
                        Required = true,
                        Default = baseUrl,
                        Validation = new ValidationRules()
                    }
                },
                Auth = authConfig,
                RateLimit = new RateLimitConfiguration
                {
                    RequestsPerMinute = request.DefaultRateLimit,
                    RequestsPerHour = request.DefaultRateLimit * 60,
                    RequestsPerDay = request.DefaultRateLimit * 1440,
                    Strategy = "fixed"
                },
                Cache = new CacheConfiguration { Enabled = false },
                Retry = new RetryConfiguration { Enabled = true },
                Monitoring = new MonitoringConfiguration { Enabled = true },
                CircuitBreaker = new CircuitBreakerConfiguration { Enabled = true }
            };

            // Create DataSource
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var dataSource = new DataSource
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = request.DataSourceName,
                Description = request.Description ?? parseResult.Description ?? "",
                Version = parseResult.Version,
                Protocol = 0,
                Config = JsonSerializer.Serialize(configRoot),
                Functions = JsonSerializer.Serialize(functions),
                Category = "Generated",
                Tags = JsonSerializer.Serialize(new List<string> { "swagger", "generated" }),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = "Swagger Generator",
                IsActive = true
            };

            context.DataSources.Add(dataSource);
            await context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Generated DataSource from Swagger content: {Id} - {Name}", dataSource.Id, dataSource.Name);

            return MapToDto(dataSource);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate DataSource from Swagger content");
            throw new DomainException($"Failed to generate DataSource from Swagger: {ex.Message}");
        }
    }

    public async Task<DataSourceResponseDto> GenerateFromGraphQLAsync(
        Guid tenantId,
        GenerateDataSourceFromUrlRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating DataSource from GraphQL endpoint: {Url}", request.Source);

        try
        {
            // Parse using GraphQLParser
            var parseResult = await _graphqlParser.ParseAsync(request.Source, cancellationToken);

            // Filter operations if needed
            var functions = parseResult.Functions;
            if (request.FilterOperations && request.IncludedOperations != null)
            {
                functions = functions.Where(f => request.IncludedOperations.Contains(f.Name)).ToList();
            }

            // Create auth configuration
            var authConfig = request.OverrideAuth && !string.IsNullOrEmpty(request.CustomAuthType)
                ? CreateAuthConfigFromType(request.CustomAuthType)
                : new AuthConfiguration
                {
                    Type = "bearer",
                    Details = new AuthDetails
                    {
                        KeyName = "Authorization",
                        Placement = "header",
                        Value = "Bearer ${config.bearer_token}"
                    },
                    RequiresTLS = true
                };

            // Build config root
            var configRoot = new ConfigRoot
            {
                Fields = new List<ConfigField>
                {
                    new ConfigField
                    {
                        Name = "endpoint",
                        Type = 10,
                        Label = "GraphQL Endpoint",
                        Description = "GraphQL endpoint URL",
                        Required = true,
                        Default = request.Source,
                        Validation = new ValidationRules()
                    }
                },
                Auth = authConfig,
                RateLimit = new RateLimitConfiguration
                {
                    RequestsPerMinute = request.DefaultRateLimit,
                    RequestsPerHour = request.DefaultRateLimit * 60,
                    RequestsPerDay = request.DefaultRateLimit * 1440,
                    Strategy = "fixed"
                },
                Cache = new CacheConfiguration { Enabled = false },
                Retry = new RetryConfiguration { Enabled = true },
                Monitoring = new MonitoringConfiguration { Enabled = true },
                CircuitBreaker = new CircuitBreakerConfiguration { Enabled = true }
            };

            // Create DataSource
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var dataSource = new DataSource
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = request.DataSourceName,
                Description = request.Description,
                Version = "1.0.0",
                Protocol = 1, // GraphQL
                Config = JsonSerializer.Serialize(configRoot),
                Functions = JsonSerializer.Serialize(functions),
                Category = "Generated",
                Tags = JsonSerializer.Serialize(new List<string> { "graphql", "generated" }),
                Metadata = JsonSerializer.Serialize(new MetadataInfo
                {
                    Documentation = $"Query Type: {parseResult.QueryType}, Mutation Type: {parseResult.MutationType}"
                }),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = "GraphQL Generator",
                IsActive = true
            };

            context.DataSources.Add(dataSource);
            await context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Generated DataSource from GraphQL: {Id} - {Name}", dataSource.Id, dataSource.Name);

            return MapToDto(dataSource);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate DataSource from GraphQL endpoint: {Url}", request.Source);
            throw new DomainException($"Failed to generate DataSource from GraphQL: {ex.Message}");
        }
    }

    public async Task<DataSourceResponseDto> GenerateFromWsdlAsync(
        Guid tenantId,
        GenerateDataSourceFromUrlRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating DataSource from WSDL: {Url}", request.Source);

        try
        {
            // Fetch WSDL content
            var httpClient = _httpClientFactory.CreateClient();
            var wsdlContent = await httpClient.GetStringAsync(request.Source, cancellationToken);

            // Parse using WsdlParser
            var parseResult = _wsdlParser.Parse(wsdlContent);

            // Filter operations if needed
            var functions = parseResult.Functions;
            if (request.FilterOperations && request.IncludedOperations != null)
            {
                functions = functions.Where(f => request.IncludedOperations.Contains(f.Name)).ToList();
            }

            // Create auth configuration
            var authConfig = request.OverrideAuth && !string.IsNullOrEmpty(request.CustomAuthType)
                ? CreateAuthConfigFromType(request.CustomAuthType)
                : new AuthConfiguration { Type = "basic" };

            // Build config root
            var configRoot = new ConfigRoot
            {
                Fields = new List<ConfigField>
                {
                    new ConfigField
                    {
                        Name = "wsdl_url",
                        Type = 10,
                        Label = "WSDL URL",
                        Description = "SOAP WSDL endpoint URL",
                        Required = true,
                        Default = parseResult.EndpointUrl ?? request.Source,
                        Validation = new ValidationRules()
                    }
                },
                Auth = authConfig,
                RateLimit = new RateLimitConfiguration
                {
                    RequestsPerMinute = request.DefaultRateLimit,
                    RequestsPerHour = request.DefaultRateLimit * 60,
                    RequestsPerDay = request.DefaultRateLimit * 1440,
                    Strategy = "fixed"
                },
                Cache = new CacheConfiguration { Enabled = false },
                Retry = new RetryConfiguration { Enabled = true },
                Monitoring = new MonitoringConfiguration { Enabled = true },
                CircuitBreaker = new CircuitBreakerConfiguration { Enabled = true }
            };

            // Create DataSource
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var dataSource = new DataSource
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = request.DataSourceName,
                Description = request.Description ?? parseResult.ServiceName,
                Version = "1.0.0",
                Protocol = 2, // SOAP
                Config = JsonSerializer.Serialize(configRoot),
                Functions = JsonSerializer.Serialize(functions),
                Category = "Generated",
                Tags = JsonSerializer.Serialize(new List<string> { "soap", "wsdl", "generated" }),
                Metadata = JsonSerializer.Serialize(new MetadataInfo
                {
                    Documentation = parseResult.TargetNamespace
                }),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = "WSDL Generator",
                IsActive = true
            };

            context.DataSources.Add(dataSource);
            await context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Generated DataSource from WSDL: {Id} - {Name}", dataSource.Id, dataSource.Name);

            return MapToDto(dataSource);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate DataSource from WSDL: {Url}", request.Source);
            throw new DomainException($"Failed to generate DataSource from WSDL: {ex.Message}");
        }
    }

    // ==================== DATASOURCE RETRIEVAL ====================

    public async Task<DataSourceResponseDto> GetByIdAsync(
        Guid tenantId,
        Guid dataSourceId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var dataSource = await context.DataSources
            .FirstOrDefaultAsync(ds => ds.Id == dataSourceId && ds.TenantId == tenantId && !ds.IsDeleted, cancellationToken);

        if (dataSource == null)
            throw new NotFoundException(nameof(DataSource), dataSourceId);

        return MapToDto(dataSource);
    }

    public async Task<List<DataSourceResponseDto>> GetAllAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var dataSources = await context.DataSources
            .Where(ds => ds.TenantId == tenantId && !ds.IsDeleted && ds.IsActive)
            .OrderBy(ds => ds.Name)
            .ToListAsync(cancellationToken);

        return dataSources.Select(MapToDto).ToList();
    }

    public async Task<List<DataSourceResponseDto>> GetByCategoryAsync(
        Guid tenantId,
        string category,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var dataSources = await context.DataSources
            .Where(ds => ds.TenantId == tenantId && ds.Category == category && !ds.IsDeleted && ds.IsActive)
            .OrderBy(ds => ds.Name)
            .ToListAsync(cancellationToken);

        return dataSources.Select(MapToDto).ToList();
    }

    public async Task<List<DataSourceResponseDto>> GetByProtocolAsync(
        Guid tenantId,
        int protocol,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var dataSources = await context.DataSources
            .Where(ds => ds.TenantId == tenantId && ds.Protocol == protocol && !ds.IsDeleted && ds.IsActive)
            .OrderBy(ds => ds.Name)
            .ToListAsync(cancellationToken);

        return dataSources.Select(MapToDto).ToList();
    }

    // ==================== FUNCTION MANAGEMENT ====================

    public async Task<List<FunctionDefinitionDto>> GetFunctionsAsync(
        Guid tenantId,
        Guid dataSourceId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var dataSource = await context.DataSources
            .FirstOrDefaultAsync(ds => ds.Id == dataSourceId && ds.TenantId == tenantId && !ds.IsDeleted, cancellationToken);

        if (dataSource == null)
            throw new NotFoundException(nameof(DataSource), dataSourceId);

        var functions = JsonSerializer.Deserialize<List<FunctionDefinition>>(dataSource.Functions) ?? new();

        return functions.Select(MapFunctionToDto).ToList();
    }

    public async Task<FunctionDefinitionDto> GetFunctionByIdAsync(
        Guid tenantId,
        Guid dataSourceId,
        string functionId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var dataSource = await context.DataSources
            .FirstOrDefaultAsync(ds => ds.Id == dataSourceId && ds.TenantId == tenantId && !ds.IsDeleted, cancellationToken);

        if (dataSource == null)
            throw new NotFoundException(nameof(DataSource), dataSourceId);

        var functions = JsonSerializer.Deserialize<List<FunctionDefinition>>(dataSource.Functions) ?? new();
        var function = functions.FirstOrDefault(f => f.Id == functionId);

        if (function == null)
            throw new NotFoundException("Function", functionId);

        return MapFunctionToDto(function);
    }

    public async Task<FunctionDefinitionDto> AddFunctionAsync(
        Guid tenantId,
        Guid dataSourceId,
        FunctionDefinitionDto functionDto,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Adding function {Name} to DataSource {Id}", functionDto.Name, dataSourceId);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var dataSource = await context.DataSources
            .FirstOrDefaultAsync(ds => ds.Id == dataSourceId && ds.TenantId == tenantId && !ds.IsDeleted, cancellationToken);

        if (dataSource == null)
            throw new NotFoundException(nameof(DataSource), dataSourceId);

        var functions = JsonSerializer.Deserialize<List<FunctionDefinition>>(dataSource.Functions) ?? new();

        if (functions.Any(f => f.Name == functionDto.Name))
            throw new DomainException($"Function with name '{functionDto.Name}' already exists");

        var newFunction = BuildFunction(functionDto);
        newFunction.Id = Guid.NewGuid().ToString();

        functions.Add(newFunction);

        dataSource.Functions = JsonSerializer.Serialize(functions);
        dataSource.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Added function {Id} to DataSource {DataSourceId}", newFunction.Id, dataSourceId);

        return MapFunctionToDto(newFunction);
    }

    public async Task<FunctionDefinitionDto> UpdateFunctionAsync(
        Guid tenantId,
        Guid dataSourceId,
        string functionId,
        FunctionDefinitionDto functionDto,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating function {Id} in DataSource {DataSourceId}", functionId, dataSourceId);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var dataSource = await context.DataSources
            .FirstOrDefaultAsync(ds => ds.Id == dataSourceId && ds.TenantId == tenantId && !ds.IsDeleted, cancellationToken);

        if (dataSource == null)
            throw new NotFoundException(nameof(DataSource), dataSourceId);

        var functions = JsonSerializer.Deserialize<List<FunctionDefinition>>(dataSource.Functions) ?? new();
        var functionIndex = functions.FindIndex(f => f.Id == functionId);

        if (functionIndex == -1)
            throw new NotFoundException("Function", functionId);

        var updatedFunction = BuildFunction(functionDto);
        updatedFunction.Id = functionId;
        functions[functionIndex] = updatedFunction;

        dataSource.Functions = JsonSerializer.Serialize(functions);
        dataSource.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated function {Id}", functionId);

        return MapFunctionToDto(updatedFunction);
    }

    public async Task DeleteFunctionAsync(
        Guid tenantId,
        Guid dataSourceId,
        string functionId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting function {Id} from DataSource {DataSourceId}", functionId, dataSourceId);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var dataSource = await context.DataSources
            .FirstOrDefaultAsync(ds => ds.Id == dataSourceId && ds.TenantId == tenantId && !ds.IsDeleted, cancellationToken);

        if (dataSource == null)
            throw new NotFoundException(nameof(DataSource), dataSourceId);

        var functions = JsonSerializer.Deserialize<List<FunctionDefinition>>(dataSource.Functions) ?? new();
        var function = functions.FirstOrDefault(f => f.Id == functionId);

        if (function == null)
            throw new NotFoundException("Function", functionId);

        var isUsed = await context.Pipelines
            .AnyAsync(p => p.DataSourceId == dataSourceId && p.FunctionId == functionId && !p.IsDeleted, cancellationToken);

        if (isUsed)
            throw new DomainException($"Cannot delete function '{function.Name}' because it is used by one or more pipelines");

        functions.Remove(function);

        dataSource.Functions = JsonSerializer.Serialize(functions);
        dataSource.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted function {Id}", functionId);
    }

    // ==================== VALIDATION & TESTING ====================

    public async Task<bool> ValidateSourceAsync(
        ValidateDataSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Validating source: {Source}", request.Source);

        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            switch (request.SourceType)
            {
                case 1: // Swagger URL
                    var swaggerResponse = await httpClient.GetAsync(request.Source, cancellationToken);
                    if (!swaggerResponse.IsSuccessStatusCode)
                        return false;

                    var swaggerContent = await swaggerResponse.Content.ReadAsStringAsync(cancellationToken);
                    
                    try
                    {
                        _swaggerParser.Parse(swaggerContent);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }

                case 3: // GraphQL
                    try
                    {
                        await _graphqlParser.ParseAsync(request.Source, cancellationToken);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }

                case 4: // SOAP WSDL
                    var wsdlResponse = await httpClient.GetAsync(request.Source, cancellationToken);
                    if (!wsdlResponse.IsSuccessStatusCode)
                        return false;

                    var wsdlContent = await wsdlResponse.Content.ReadAsStringAsync(cancellationToken);
                    
                    try
                    {
                        _wsdlParser.Parse(wsdlContent);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }

                default:
                    throw new DomainException($"Unsupported source type: {request.SourceType}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate source: {Source}", request.Source);
            return false;
        }
    }

    public async Task<object> AnalyzeSourceAsync(
        AnalyzeDataSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Analyzing source: {Source}", request.Source);

        try
        {
            var httpClient = _httpClientFactory.CreateClient();

            switch (request.SourceType)
            {
                case 1: // Swagger URL
                case 2: // Swagger Content
                    var swaggerContent = request.SourceType == 1
                        ? await httpClient.GetStringAsync(request.Source, cancellationToken)
                        : request.Source;

                    var swaggerResult = _swaggerParser.Parse(swaggerContent);

                    return new
                    {
                        SourceType = "Swagger/OpenAPI",
                        IsValid = true,
                        Title = swaggerResult.Title,
                        Version = swaggerResult.Version,
                        Description = swaggerResult.Description,
                        BaseUrl = swaggerResult.BaseUrl,
                        OperationCount = swaggerResult.Functions.Count,
                        Operations = swaggerResult.Functions.Select(f => f.Name).ToList(),
                        SecuritySchemes = swaggerResult.SecuritySchemes.Keys.ToList(),
                        AuthTypes = swaggerResult.SecuritySchemes.Select(s => s.Value.Type).Distinct().ToList()
                    };

                case 3: // GraphQL
                    var graphqlResult = await _graphqlParser.ParseAsync(request.Source, cancellationToken);

                    return new
                    {
                        SourceType = "GraphQL",
                        IsValid = true,
                        Endpoint = graphqlResult.Endpoint,
                        QueryType = graphqlResult.QueryType,
                        MutationType = graphqlResult.MutationType,
                        SubscriptionType = graphqlResult.SubscriptionType,
                        OperationCount = graphqlResult.Functions.Count,
                        Operations = graphqlResult.Functions.Select(f => f.Name).ToList(),
                        QueryCount = graphqlResult.Functions.Count(f => 
                            f.ProtocolSpecific.TryGetValue("operationType", out var opType) && 
                            opType.ToString() == "query"),
                        MutationCount = graphqlResult.Functions.Count(f => 
                            f.ProtocolSpecific.TryGetValue("operationType", out var opType) && 
                            opType.ToString() == "mutation"),
                        Types = graphqlResult.Types.Keys.ToList()
                    };

                case 4: // SOAP WSDL
                    var wsdlContent = await httpClient.GetStringAsync(request.Source, cancellationToken);
                    var wsdlResult = _wsdlParser.Parse(wsdlContent);

                    return new
                    {
                        SourceType = "SOAP/WSDL",
                        IsValid = true,
                        ServiceName = wsdlResult.ServiceName,
                        TargetNamespace = wsdlResult.TargetNamespace,
                        EndpointUrl = wsdlResult.EndpointUrl,
                        OperationCount = wsdlResult.Functions.Count,
                        Operations = wsdlResult.Functions.Select(f => f.Name).ToList(),
                        PortTypes = wsdlResult.PortTypes.Keys.ToList(),
                        Bindings = wsdlResult.Bindings.Keys.ToList()
                    };

                default:
                    throw new DomainException($"Unsupported source type: {request.SourceType}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze source: {Source}", request.Source);
            return new
            {
                SourceType = request.SourceType,
                IsValid = false,
                Error = ex.Message
            };
        }
    }

    public async Task<List<string>> GetOperationsFromSourceAsync(
        GetOperationsRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting operations from source: {Source}", request.Source);

        try
        {
            var httpClient = _httpClientFactory.CreateClient();

            switch (request.SourceType)
            {
                case 1: // Swagger URL
                case 2: // Swagger Content
                    var swaggerContent = request.SourceType == 1
                        ? await httpClient.GetStringAsync(request.Source, cancellationToken)
                        : request.Source;

                    var swaggerResult = _swaggerParser.Parse(swaggerContent);
                    return swaggerResult.Functions.Select(f => f.Name).ToList();

                case 3: // GraphQL
                    var graphqlResult = await _graphqlParser.ParseAsync(request.Source, cancellationToken);
                    return graphqlResult.Functions.Select(f => f.Name).ToList();

                case 4: // SOAP WSDL
                    var wsdlContent = await httpClient.GetStringAsync(request.Source, cancellationToken);
                    var wsdlResult = _wsdlParser.Parse(wsdlContent);
                    return wsdlResult.Functions.Select(f => f.Name).ToList();

                default:
                    throw new DomainException($"Unsupported source type: {request.SourceType}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get operations from source: {Source}", request.Source);
            throw new DomainException($"Failed to get operations: {ex.Message}");
        }
    }

    public async Task<DataSourceResponseDto> PreviewDataSourceAsync(
        PreviewDataSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Previewing DataSource from source: {Source}", request.Source);

        try
        {
            var httpClient = _httpClientFactory.CreateClient();

            switch (request.SourceType)
            {
                case 1: // Swagger URL
                    var swaggerContent = await httpClient.GetStringAsync(request.Source, cancellationToken);
                    var swaggerResult = _swaggerParser.Parse(swaggerContent);
                    
                    return CreatePreviewDto(
                        request.DataSourceName,
                        swaggerResult.Description ?? "Preview from Swagger",
                        0,
                        swaggerResult.BaseUrl,
                        swaggerResult.Functions
                    );

                case 3: // GraphQL
                    var graphqlResult = await _graphqlParser.ParseAsync(request.Source, cancellationToken);
                    
                    return CreatePreviewDto(
                        request.DataSourceName,
                        "Preview from GraphQL",
                        1,
                        request.Source,
                        graphqlResult.Functions
                    );

                case 4: // SOAP WSDL
                    var wsdlContent = await httpClient.GetStringAsync(request.Source, cancellationToken);
                    var wsdlResult = _wsdlParser.Parse(wsdlContent);
                    
                    return CreatePreviewDto(
                        request.DataSourceName,
                        wsdlResult.ServiceName,
                        2,
                        wsdlResult.EndpointUrl ?? request.Source,
                        wsdlResult.Functions
                    );

                default:
                    throw new DomainException($"Unsupported source type: {request.SourceType}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to preview DataSource");
            throw new DomainException($"Failed to preview DataSource: {ex.Message}");
        }
    }

    public async Task<object> TestFunctionAsync(
        Guid tenantId,
        Guid dataSourceId,
        string functionId,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Testing function {FunctionId} of DataSource {DataSourceId}", functionId, dataSourceId);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var dataSource = await context.DataSources
            .FirstOrDefaultAsync(ds => ds.Id == dataSourceId && ds.TenantId == tenantId && !ds.IsDeleted, cancellationToken);

        if (dataSource == null)
            throw new NotFoundException(nameof(DataSource), dataSourceId);

        var functions = JsonSerializer.Deserialize<List<FunctionDefinition>>(dataSource.Functions) ?? new();
        var function = functions.FirstOrDefault(f => f.Id == functionId);

        if (function == null)
            throw new NotFoundException("Function", functionId);

        var config = JsonSerializer.Deserialize<ConfigRoot>(dataSource.Config) ?? new();

        try
        {
            var baseUrl = GetConfigValue(config, "base_url") ?? GetConfigValue(config, "endpoint") ?? "";
            var url = BuildFunctionUrl(baseUrl, function.Path, parameters);

            var httpClient = _httpClientFactory.CreateClient();

            ApplyAuthentication(httpClient, config, parameters);

            if (!string.IsNullOrEmpty(dataSource.Headers))
            {
                var headers = JsonSerializer.Deserialize<List<HeaderDefinition>>(dataSource.Headers);
                if (headers != null)
                {
                    foreach (var header in headers.Where(h => h.Required))
                    {
                        var headerValue = ResolveTemplateValue(header.Value, config, parameters);
                        httpClient.DefaultRequestHeaders.Add(header.Name, headerValue);
                    }
                }
            }

            HttpResponseMessage response;
            switch (function.Method.ToUpper())
            {
                case "GET":
                    response = await httpClient.GetAsync(url, cancellationToken);
                    break;

                case "POST":
                    var postContent = BuildRequestBody(function, parameters);
                    response = await httpClient.PostAsync(url, postContent, cancellationToken);
                    break;

                case "PUT":
                    var putContent = BuildRequestBody(function, parameters);
                    response = await httpClient.PutAsync(url, putContent, cancellationToken);
                    break;

                case "DELETE":
                    response = await httpClient.DeleteAsync(url, cancellationToken);
                    break;

                case "PATCH":
                    var patchContent = BuildRequestBody(function, parameters);
                    response = await httpClient.PatchAsync(url, patchContent, cancellationToken);
                    break;

                default:
                    throw new DomainException($"Unsupported HTTP method: {function.Method}");
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            return new
            {
                Success = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                Function = function.Name,
                Url = url,
                Method = function.Method,
                Response = responseContent,
                Headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value))
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Function test failed");
            return new
            {
                Success = false,
                Function = function.Name,
                Error = ex.Message,
                Parameters = parameters
            };
        }
    }

    public async Task<bool> TestConnectionAsync(
        Guid tenantId,
        Guid dataSourceId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Testing connection for DataSource {Id}", dataSourceId);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var dataSource = await context.DataSources
            .FirstOrDefaultAsync(ds => ds.Id == dataSourceId && ds.TenantId == tenantId && !ds.IsDeleted, cancellationToken);

        if (dataSource == null)
            throw new NotFoundException(nameof(DataSource), dataSourceId);

        try
        {
            var config = JsonSerializer.Deserialize<ConfigRoot>(dataSource.Config) ?? new();
            var baseUrl = GetConfigValue(config, "base_url") ?? GetConfigValue(config, "endpoint") ?? "";

            if (string.IsNullOrEmpty(baseUrl))
            {
                throw new DomainException("No base URL configured for DataSource");
            }

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            var response = await httpClient.GetAsync(baseUrl, cancellationToken);

            dataSource.LastTestedAt = DateTime.UtcNow;
            dataSource.LastTestResult = response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Unauthorized;
            dataSource.LastTestError = response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}";

            await context.SaveChangesAsync(cancellationToken);

            return dataSource.LastTestResult ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed for DataSource {Id}", dataSourceId);

            dataSource.LastTestedAt = DateTime.UtcNow;
            dataSource.LastTestResult = false;
            dataSource.LastTestError = ex.Message;

            await context.SaveChangesAsync(cancellationToken);

            return false;
        }
    }

    // ==================== LIFECYCLE MANAGEMENT ====================

    public async Task ActivateAsync(
        Guid tenantId,
        Guid dataSourceId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Activating DataSource {Id}", dataSourceId);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var dataSource = await context.DataSources
            .FirstOrDefaultAsync(ds => ds.Id == dataSourceId && ds.TenantId == tenantId && !ds.IsDeleted, cancellationToken);

        if (dataSource == null)
            throw new NotFoundException(nameof(DataSource), dataSourceId);

        dataSource.IsActive = true;
        dataSource.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeactivateAsync(
        Guid tenantId,
        Guid dataSourceId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deactivating DataSource {Id}", dataSourceId);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var dataSource = await context.DataSources
            .FirstOrDefaultAsync(ds => ds.Id == dataSourceId && ds.TenantId == tenantId && !ds.IsDeleted, cancellationToken);

        if (dataSource == null)
            throw new NotFoundException(nameof(DataSource), dataSourceId);

        dataSource.IsActive = false;
        dataSource.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        Guid tenantId,
        Guid dataSourceId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting DataSource {Id}", dataSourceId);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var dataSource = await context.DataSources
            .FirstOrDefaultAsync(ds => ds.Id == dataSourceId && ds.TenantId == tenantId && !ds.IsDeleted, cancellationToken);

        if (dataSource == null)
            throw new NotFoundException(nameof(DataSource), dataSourceId);

        var isUsed = await context.Pipelines
            .AnyAsync(p => p.DataSourceId == dataSourceId && !p.IsDeleted, cancellationToken);

        if (isUsed)
            throw new DomainException("Cannot delete DataSource because it is used by one or more collectors");

        dataSource.IsDeleted = true;
        dataSource.DeletedAt = DateTime.UtcNow;
        dataSource.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<DataSourceResponseDto> CloneAsync(
        Guid tenantId,
        Guid dataSourceId,
        string newName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Cloning DataSource {Id} to {NewName}", dataSourceId, newName);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var original = await context.DataSources
            .FirstOrDefaultAsync(ds => ds.Id == dataSourceId && ds.TenantId == tenantId && !ds.IsDeleted, cancellationToken);

        if (original == null)
            throw new NotFoundException(nameof(DataSource), dataSourceId);

        var exists = await context.DataSources
            .AnyAsync(ds => ds.TenantId == tenantId && ds.Name == newName && !ds.IsDeleted, cancellationToken);

        if (exists)
            throw new DomainException($"DataSource with name '{newName}' already exists");

        var clone = new DataSource
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = newName,
            Description = original.Description + " (Clone)",
            Version = original.Version,
            DatasourceVersion = original.DatasourceVersion,
            ImageUrl = original.ImageUrl,
            Protocol = original.Protocol,
            Config = original.Config,
            Headers = original.Headers,
            Body = original.Body,
            Functions = original.Functions,
            Category = original.Category,
            Tags = original.Tags,
            Metadata = original.Metadata,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = "System",
            IsActive = false
        };

        context.DataSources.Add(clone);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cloned DataSource {OriginalId} to {CloneId}", dataSourceId, clone.Id);

        return MapToDto(clone);
    }

    public async Task<string> ExportAsync(
        Guid tenantId,
        Guid dataSourceId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Exporting DataSource {Id}", dataSourceId);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var dataSource = await context.DataSources
            .FirstOrDefaultAsync(ds => ds.Id == dataSourceId && ds.TenantId == tenantId && !ds.IsDeleted, cancellationToken);

        if (dataSource == null)
            throw new NotFoundException(nameof(DataSource), dataSourceId);

        var dto = MapToDto(dataSource);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Serialize(dto, options);
    }

    public async Task<DataSourceResponseDto> ImportAsync(
        Guid tenantId,
        string dataSourceJson,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Importing DataSource for tenant {TenantId}", tenantId);

        var dto = JsonSerializer.Deserialize<DataSourceResponseDto>(dataSourceJson);

        if (dto == null)
            throw new DomainException("Invalid DataSource JSON");

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var exists = await context.DataSources
            .AnyAsync(ds => ds.TenantId == tenantId && ds.Name == dto.Name && !ds.IsDeleted, cancellationToken);

        if (exists)
            throw new DomainException($"DataSource with name '{dto.Name}' already exists");

        var dataSource = new DataSource
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = dto.Name,
            Description = dto.Description,
            Version = dto.Version,
            DatasourceVersion = dto.DatasourceVersion,
            ImageUrl = dto.ImageUrl,
            Protocol = dto.Protocol,
            Config = JsonSerializer.Serialize(dto.Config),
            Headers = dto.Headers != null ? JsonSerializer.Serialize(dto.Headers) : null,
            Body = dto.Body != null ? JsonSerializer.Serialize(dto.Body) : null,
            Functions = JsonSerializer.Serialize(dto.Functions),
            Category = dto.Category,
            Tags = dto.Tags != null ? JsonSerializer.Serialize(dto.Tags) : null,
            Metadata = dto.Metadata != null ? JsonSerializer.Serialize(dto.Metadata) : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = "Import",
            IsActive = false
        };

        context.DataSources.Add(dataSource);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Imported DataSource {Id} - {Name}", dataSource.Id, dataSource.Name);

        return MapToDto(dataSource);
    }

    // ==================== HELPER METHODS ====================

    private AuthConfiguration CreateAuthFromSecuritySchemes(
        Dictionary<string, SecurityScheme> securitySchemes,
        GenerateDataSourceFromUrlRequest request)
    {
        if (request.OverrideAuth && !string.IsNullOrEmpty(request.CustomAuthType))
        {
            return CreateAuthConfigFromType(request.CustomAuthType);
        }

        var firstScheme = securitySchemes.FirstOrDefault();
        if (firstScheme.Value != null)
        {
            var scheme = firstScheme.Value;
            
            switch (scheme.Type.ToLower())
            {
                case "apikey":
                    return new AuthConfiguration
                    {
                        Type = "api_key",
                        Details = new AuthDetails
                        {
                            KeyName = scheme.Name ?? "Authorization",
                            Placement = scheme.In ?? "header",
                            Value = "${config.api_key}"
                        },
                        RequiresTLS = true
                    };

                case "http":
                    if (scheme.Scheme?.ToLower() == "bearer")
                    {
                        return new AuthConfiguration
                        {
                            Type = "bearer",
                            Details = new AuthDetails
                            {
                                KeyName = "Authorization",
                                Placement = "header",
                                Value = "Bearer ${config.bearer_token}"
                            },
                            RequiresTLS = true
                        };
                    }
                    else if (scheme.Scheme?.ToLower() == "basic")
                    {
                        return new AuthConfiguration
                        {
                            Type = "basic",
                            RequiresTLS = true
                        };
                    }
                    break;

                case "oauth2":
                    return new AuthConfiguration
                    {
                        Type = "oauth2",
                        RequiresTLS = true
                    };
            }
        }

        return new AuthConfiguration { Type = "none" };
    }

    private AuthConfiguration CreateAuthConfigFromType(string authType)
    {
        return authType.ToLower() switch
        {
            "api_key" => new AuthConfiguration
            {
                Type = "api_key",
                Details = new AuthDetails
                {
                    KeyName = "Authorization",
                    Placement = "header",
                    Value = "${config.api_key}"
                }
            },
            "bearer" => new AuthConfiguration { Type = "bearer" },
            "basic" => new AuthConfiguration { Type = "basic" },
            "oauth2" => new AuthConfiguration { Type = "oauth2" },
            _ => new AuthConfiguration { Type = "none" }
        };
    }

    private DataSourceResponseDto CreatePreviewDto(
        string name,
        string description,
        int protocol,
        string? baseUrl,
        List<FunctionDefinition> functions)
    {
        return new DataSourceResponseDto
        {
            Id = Guid.Empty,
            Name = name,
            Description = description,
            Version = "1.0.0",
            Protocol = protocol,
            Config = new ConfigRoot
            {
                Fields = new List<ConfigField>
                {
                    new ConfigField
                    {
                        Name = "base_url",
                        Type = 10,
                        Label = "Base URL",
                        Required = true,
                        Default = baseUrl ?? ""
                    }
                }
            },
            Functions = functions,
            Category = "Preview",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = "Preview",
            IsActive = false
        };
    }

    private string BuildFunctionUrl(string baseUrl, string path, Dictionary<string, object> parameters)
    {
        var url = baseUrl.TrimEnd('/') + "/" + path.TrimStart('/');

        foreach (var param in parameters)
        {
            url = url.Replace($"{{{param.Key}}}", param.Value?.ToString() ?? "");
        }

        var queryParams = parameters
            .Where(p => !url.Contains($"{{{p.Key}}}"))
            .Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value?.ToString() ?? "")}");

        if (queryParams.Any())
        {
            url += "?" + string.Join("&", queryParams);
        }

        return url;
    }

    private void ApplyAuthentication(HttpClient httpClient, ConfigRoot config, Dictionary<string, object> parameters)
    {
        var auth = config.Auth;

        switch (auth.Type.ToLower())
        {
            case "api_key":
                var apiKeyValue = ResolveTemplateValue(auth.Details.Value, config, parameters);
                if (auth.Details.Placement == "header")
                {
                    httpClient.DefaultRequestHeaders.Add(auth.Details.KeyName, apiKeyValue);
                }
                break;

            case "bearer":
                var bearerToken = GetConfigValue(config, "bearer_token") ?? "";
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
                break;

            case "basic":
                var username = GetConfigValue(config, "username") ?? "";
                var password = GetConfigValue(config, "password") ?? "";
                var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                break;
        }
    }

    private StringContent BuildRequestBody(FunctionDefinition function, Dictionary<string, object> parameters)
    {
        var body = JsonSerializer.Serialize(parameters);
        return new StringContent(body, Encoding.UTF8, function.RequestBody.ContentType);
    }

    private string ResolveTemplateValue(string template, ConfigRoot config, Dictionary<string, object> parameters)
    {
        if (template.StartsWith("${config."))
        {
            var fieldName = template.Substring(9, template.Length - 10);
            return GetConfigValue(config, fieldName) ?? "";
        }

        if (template.StartsWith("${param."))
        {
            var paramName = template.Substring(8, template.Length - 9);
            return parameters.TryGetValue(paramName, out var value) ? value?.ToString() ?? "" : "";
        }

        return template;
    }

    private string? GetConfigValue(ConfigRoot config, string fieldName)
    {
        var field = config.Fields.FirstOrDefault(f => f.Name == fieldName);
        return field?.Default;
    }

    private ConfigRoot BuildConfigRoot(CreateManualDataSourceRequest request)
    {
        return new ConfigRoot
        {
            Fields = request.ConfigFields.Select(f => new ConfigField
            {
                Name = f.Name,
                Type = f.Type,
                Label = f.Label,
                Description = f.Description,
                Required = f.Required,
                Default = f.Default,
                Options = f.Options.Select(o => new ConfigFieldOption
                {
                    Value = o.Value,
                    Label = o.Label,
                    Description = o.Description
                }).ToList(),
                Validation = new ValidationRules
                {
                    Pattern = f.Validation.Pattern,
                    Min = f.Validation.Min,
                    Max = f.Validation.Max,
                    MinLength = f.Validation.MinLength,
                    MaxLength = f.Validation.MaxLength,
                    AllowedValues = f.Validation.AllowedValues,
                    CustomValidator = f.Validation.CustomValidator
                },
                Encrypted = f.Encrypted,
                DependsOn = f.DependsOn,
                Metadata = f.Metadata
            }).ToList(),
            Auth = new AuthConfiguration
            {
                Type = request.Auth.Type,
                Details = new AuthDetails
                {
                    KeyName = request.Auth.Details.KeyName,
                    Placement = request.Auth.Details.Placement,
                    Value = request.Auth.Details.Value
                },
                RequiresTLS = request.Auth.RequiresTLS,
                TokenExpirationMinutes = request.Auth.TokenExpirationMinutes,
                RefreshTokenSupported = request.Auth.RefreshTokenSupported
            },
            RateLimit = request.RateLimit != null ? new RateLimitConfiguration
            {
                RequestsPerMinute = request.RateLimit.RequestsPerMinute,
                RequestsPerHour = request.RateLimit.RequestsPerHour,
                RequestsPerDay = request.RateLimit.RequestsPerDay,
                Strategy = request.RateLimit.Strategy
            } : new RateLimitConfiguration(),
            Cache = request.Cache != null ? new CacheConfiguration
            {
                Enabled = request.Cache.Enabled,
                TtlSeconds = request.Cache.TtlSeconds,
                CacheableOperations = request.Cache.CacheableOperations,
                Strategy = request.Cache.Strategy
            } : new CacheConfiguration(),
            Retry = request.Retry != null ? new RetryConfiguration
            {
                Enabled = request.Retry.Enabled,
                MaxAttempts = request.Retry.MaxAttempts,
                InitialDelayMs = request.Retry.InitialDelayMs,
                BackoffStrategy = request.Retry.BackoffStrategy,
                RetryableStatusCodes = request.Retry.RetryableStatusCodes
            } : new RetryConfiguration(),
            Monitoring = request.Monitoring != null ? new MonitoringConfiguration
            {
                Enabled = request.Monitoring.Enabled,
                EnableMetrics = request.Monitoring.EnableMetrics,
                EnableTracing = request.Monitoring.EnableTracing,
                EnableHealthChecks = request.Monitoring.EnableHealthChecks,
                MetricsEnabled = request.Monitoring.MetricsEnabled,
                HealthCheckEnabled = request.Monitoring.HealthCheckEnabled,
                MetricsIntervalSeconds = request.Monitoring.MetricsIntervalSeconds,
                MetricsEndpoint = request.Monitoring.MetricsEndpoint,
                HealthCheckEndpoint = request.Monitoring.HealthCheckEndpoint,
                HealthCheckPath = request.Monitoring.HealthCheckPath,
                CustomMetrics = request.Monitoring.CustomMetrics,
                Logging = new LoggingConfiguration
                {
                    LogRequests = request.Monitoring.Logging.LogRequests,
                    LogResponses = request.Monitoring.Logging.LogResponses,
                    LogErrors = request.Monitoring.Logging.LogErrors,
                    LogPerformance = request.Monitoring.Logging.LogPerformance,
                    LogLevel = request.Monitoring.Logging.LogLevel,
                    SanitizeSensitiveData = request.Monitoring.Logging.SanitizeSensitiveData,
                    SensitiveFields = request.Monitoring.Logging.SensitiveFields
                },
                Alerting = new AlertingConfiguration
                {
                    Enabled = request.Monitoring.Alerting.Enabled,
                    Rules = request.Monitoring.Alerting.Rules,
                    Notifications = new NotificationConfiguration
                    {
                        Channels = request.Monitoring.Alerting.Notifications.Channels,
                        Settings = request.Monitoring.Alerting.Notifications.Settings
                    },
                    ErrorRateThreshold = request.Monitoring.Alerting.ErrorRateThreshold,
                    LatencyThresholdMs = request.Monitoring.Alerting.LatencyThresholdMs,
                    AvailabilityThreshold = request.Monitoring.Alerting.AvailabilityThreshold
                }
            } : new MonitoringConfiguration(),
            CircuitBreaker = request.CircuitBreaker != null ? new CircuitBreakerConfiguration
            {
                Enabled = request.CircuitBreaker.Enabled,
                FailureThreshold = request.CircuitBreaker.FailureThreshold,
                SuccessThreshold = request.CircuitBreaker.SuccessThreshold,
                TimeoutMs = request.CircuitBreaker.TimeoutMs,
                ResetTimeoutMs = request.CircuitBreaker.ResetTimeoutMs,
                RecoveryTimeoutMs = request.CircuitBreaker.RecoveryTimeoutMs,
                TrackedStatusCodes = request.CircuitBreaker.TrackedStatusCodes,
                TrackedExceptions = request.CircuitBreaker.TrackedExceptions,
                LogStateChanges = request.CircuitBreaker.LogStateChanges,
                Strategy = request.CircuitBreaker.Strategy
            } : new CircuitBreakerConfiguration()
        };
    }

    private List<FunctionDefinition> BuildFunctions(List<FunctionDefinitionDto> functionDtos)
    {
        return functionDtos.Select(BuildFunction).ToList();
    }

    private FunctionDefinition BuildFunction(FunctionDefinitionDto dto)
    {
        return new FunctionDefinition
        {
            Id = dto.Id,
            Name = dto.Name,
            Description = dto.Description,
            Method = dto.Method,
            Path = dto.Path,
            Parameters = dto.Parameters.Select(p => new FunctionParameter
            {
                Name = p.Name,
                Type = p.Type,
                Location = p.Location,
                Required = p.Required,
                Description = p.Description,
                Default = p.Default,
                Validation = new ValidationRules
                {
                    Pattern = p.Validation.Pattern,
                    Min = p.Validation.Min,
                    Max = p.Validation.Max,
                    MinLength = p.Validation.MinLength,
                    MaxLength = p.Validation.MaxLength,
                    AllowedValues = p.Validation.AllowedValues,
                    CustomValidator = p.Validation.CustomValidator
                },
                Examples = p.Examples
            }).ToList(),
            RequestBody = new FunctionRequestBody
            {
                Schema = dto.RequestBody.Schema,
                TemplateRef = dto.RequestBody.TemplateRef,
                ContentType = dto.RequestBody.ContentType,
                Required = dto.RequestBody.Required
            },
            Response = new FunctionResponse
            {
                ExpectedFormat = dto.Response.ExpectedFormat,
                Schema = dto.Response.Schema,
                StatusCodes = dto.Response.StatusCodes.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new ResponseStatusCode
                    {
                        Description = kvp.Value.Description,
                        Schema = kvp.Value.Schema
                    }),
                Headers = dto.Response.Headers.Select(h => new HeaderDefinition
                {
                    Name = h.Name,
                    Value = h.Value,
                    Required = h.Required,
                    IsDynamic = h.IsDynamic
                }).ToList()
            },
            ProtocolSpecific = dto.ProtocolSpecific,
            RequiresAuth = dto.RequiresAuth,
            Scopes = dto.Scopes,
            Timeout = dto.Timeout,
            IsDeprecated = dto.IsDeprecated,
            DeprecationMessage = dto.DeprecationMessage
        };
    }

    private DataSourceResponseDto MapToDto(DataSource dataSource)
    {
        var config = JsonSerializer.Deserialize<ConfigRoot>(dataSource.Config) ?? new();
        var functions = JsonSerializer.Deserialize<List<FunctionDefinition>>(dataSource.Functions) ?? new();
        var headers = !string.IsNullOrEmpty(dataSource.Headers)
            ? JsonSerializer.Deserialize<List<HeaderDefinition>>(dataSource.Headers)
            : null;
        var body = !string.IsNullOrEmpty(dataSource.Body)
            ? JsonSerializer.Deserialize<BodyConfiguration>(dataSource.Body)
            : null;
        var tags = !string.IsNullOrEmpty(dataSource.Tags)
            ? JsonSerializer.Deserialize<List<string>>(dataSource.Tags)
            : null;
        var metadata = !string.IsNullOrEmpty(dataSource.Metadata)
            ? JsonSerializer.Deserialize<MetadataInfo>(dataSource.Metadata)
            : null;

        return new DataSourceResponseDto
        {
            Id = dataSource.Id,
            Name = dataSource.Name,
            Description = dataSource.Description,
            Version = dataSource.Version,
            DatasourceVersion = dataSource.DatasourceVersion,
            ImageUrl = dataSource.ImageUrl,
            Protocol = dataSource.Protocol,
            Config = config,
            Headers = headers,
            Body = body,
            Functions = functions,
            Category = dataSource.Category,
            Tags = tags,
            Metadata = metadata,
            CreatedAt = dataSource.CreatedAt,
            UpdatedAt = dataSource.UpdatedAt,
            CreatedBy = dataSource.CreatedBy,
            IsActive = dataSource.IsActive
        };
    }

    private FunctionDefinitionDto MapFunctionToDto(FunctionDefinition function)
    {
        return new FunctionDefinitionDto
        {
            Id = function.Id,
            Name = function.Name,
            Description = function.Description,
            Method = function.Method,
            Path = function.Path,
            Parameters = function.Parameters.Select(p => new FunctionParameterDto
            {
                Name = p.Name,
                Type = p.Type,
                Location = p.Location,
                Required = p.Required,
                Description = p.Description,
                Default = p.Default,
                Validation = new ValidationRulesDto
                {
                    Pattern = p.Validation.Pattern,
                    Min = p.Validation.Min,
                    Max = p.Validation.Max,
                    MinLength = p.Validation.MinLength,
                    MaxLength = p.Validation.MaxLength,
                    AllowedValues = p.Validation.AllowedValues,
                    CustomValidator = p.Validation.CustomValidator
                },
                Examples = p.Examples
            }).ToList(),
            RequestBody = new FunctionRequestBodyDto
            {
                Schema = function.RequestBody.Schema,
                TemplateRef = function.RequestBody.TemplateRef,
                ContentType = function.RequestBody.ContentType,
                Required = function.RequestBody.Required
            },
            Response = new FunctionResponseDto
            {
                ExpectedFormat = function.Response.ExpectedFormat,
                Schema = function.Response.Schema,
                StatusCodes = function.Response.StatusCodes.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new ResponseStatusCodeDto
                    {
                        Description = kvp.Value.Description,
                        Schema = kvp.Value.Schema
                    }),
                Headers = function.Response.Headers.Select(h => new HeaderDefinitionDto
                {
                    Name = h.Name,
                    Value = h.Value,
                    Required = h.Required,
                    IsDynamic = h.IsDynamic
                }).ToList()
            },
            ProtocolSpecific = function.ProtocolSpecific,
            RequiresAuth = function.RequiresAuth,
            Scopes = function.Scopes,
            Timeout = function.Timeout,
            IsDeprecated = function.IsDeprecated,
            DeprecationMessage = function.DeprecationMessage
        };
    }
}