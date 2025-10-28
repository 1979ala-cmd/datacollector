# Migration Guide: Simple Pipelines → Function-Based Architecture

## 📋 Overview

This guide explains how to migrate from the simple pipeline structure (where pipelines stored API paths directly) to the new function-based architecture (where DataSources define functions and pipelines reference them).

---

## 🔄 What's Changing

### Old Structure
```json
{
  "collector": {
    "name": "Customer Collector",
    "pipelines": [
      {
        "dataSourceId": "ds-123",
        "apiPath": "/api/customers",
        "method": "GET",
        "parameters": {...}
      }
    ]
  }
}
```

### New Structure
```json
{
  "dataSource": {
    "id": "ds-123",
    "functions": [
      {
        "id": "func-get-customers",
        "name": "getCustomers",
        "method": "GET",
        "path": "/api/customers",
        "parameters": [...]
      }
    ]
  },
  "collector": {
    "name": "Customer Collector",
    "dataSourceId": "ds-123",
    "pipelines": [
      {
        "functionId": "func-get-customers",
        "parameterMappings": {...},
        "staticParameters": {...}
      }
    ]
  }
}
```

---

## 🗃️ Database Migration

### Step 1: Update DataSource Table

```sql
-- Add new columns to data_sources table
ALTER TABLE data_sources
ADD COLUMN source TEXT,
ADD COLUMN base_url VARCHAR(500),
ADD COLUMN config_fields JSONB,
ADD COLUMN auth_config JSONB,
ADD COLUMN headers JSONB,
ADD COLUMN functions JSONB NOT NULL DEFAULT '[]'::jsonb,
ADD COLUMN rate_limit_config JSONB,
ADD COLUMN cache_config JSONB,
ADD COLUMN retry_config JSONB,
ADD COLUMN monitoring_config JSONB,
ADD COLUMN circuit_breaker_config JSONB,
ADD COLUMN category VARCHAR(100),
ADD COLUMN tags JSONB,
ADD COLUMN metadata JSONB,
ADD COLUMN last_test_error TEXT;

-- Rename old columns if they exist
ALTER TABLE data_sources
RENAME COLUMN endpoint TO base_url_old;

ALTER TABLE data_sources
RENAME COLUMN config TO config_old;
```

### Step 2: Update Pipeline Table

```sql
-- Add new columns to pipelines table
ALTER TABLE pipelines
ADD COLUMN function_id VARCHAR(100),
ADD COLUMN function_name VARCHAR(200),
ADD COLUMN parameter_mappings JSONB,
ADD COLUMN static_parameters JSONB,
ADD COLUMN data_ingestion JSONB;

-- Make function_id NOT NULL after migration
-- (Do this after data migration is complete)
-- ALTER TABLE pipelines
-- ALTER COLUMN function_id SET NOT NULL;

-- Rename old columns
ALTER TABLE pipelines
RENAME COLUMN parameters TO parameters_old;
```

---

## 📊 Data Migration Script

### Step 3: Migrate Existing Data

```csharp
using System.Text.Json;
using Npgsql;

public class DataMigrationService
{
    private readonly string _connectionString;
    
    public async Task MigrateDataSourcesAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        
        // Get all existing DataSources
        var dataSources = await GetExistingDataSourcesAsync(connection);
        
        foreach (var dataSource in dataSources)
        {
            // Generate functions from pipelines using this DataSource
            var functions = await GenerateFunctionsFromPipelinesAsync(
                connection, 
                dataSource.Id
            );
            
            // Update DataSource with functions
            await UpdateDataSourceWithFunctionsAsync(
                connection,
                dataSource.Id,
                functions
            );
            
            Console.WriteLine($"Migrated DataSource: {dataSource.Name} with {functions.Count} functions");
        }
    }
    
    private async Task<List<DataSourceInfo>> GetExistingDataSourcesAsync(
        NpgsqlConnection connection)
    {
        var dataSources = new List<DataSourceInfo>();
        
        var sql = @"
            SELECT id, name, base_url_old, config_old, auth_type, auth_config
            FROM data_sources
            WHERE NOT is_deleted
        ";
        
        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            dataSources.Add(new DataSourceInfo
            {
                Id = reader.GetGuid(0),
                Name = reader.GetString(1),
                BaseUrl = reader.IsDBNull(2) ? null : reader.GetString(2),
                OldConfig = reader.IsDBNull(3) ? null : reader.GetString(3),
                AuthType = reader.IsDBNull(4) ? null : reader.GetString(4),
                AuthConfig = reader.IsDBNull(5) ? null : reader.GetString(5)
            });
        }
        
        return dataSources;
    }
    
    private async Task<List<FunctionDefinition>> GenerateFunctionsFromPipelinesAsync(
        NpgsqlConnection connection,
        Guid dataSourceId)
    {
        var functions = new List<FunctionDefinition>();
        var functionIdMap = new Dictionary<string, string>(); // path+method -> functionId
        
        var sql = @"
            SELECT id, name, api_path, method, parameters_old
            FROM pipelines
            WHERE data_source_id = @dataSourceId
            AND NOT is_deleted
        ";
        
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("dataSourceId", dataSourceId);
        await using var reader = await cmd.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            var pipelineId = reader.GetGuid(0);
            var pipelineName = reader.GetString(1);
            var apiPath = reader.GetString(2);
            var method = reader.GetString(3);
            var parametersJson = reader.IsDBNull(4) ? null : reader.GetString(4);
            
            // Create unique key for this endpoint
            var key = $"{method}:{apiPath}";
            
            // If we haven't seen this endpoint before, create a function
            if (!functionIdMap.ContainsKey(key))
            {
                var functionId = $"func-{Guid.NewGuid().ToString("N").Substring(0, 12)}";
                functionIdMap[key] = functionId;
                
                var function = new FunctionDefinition
                {
                    Id = functionId,
                    Name = GenerateFunctionName(apiPath, method),
                    Description = $"Generated from pipeline: {pipelineName}",
                    Method = method,
                    Path = apiPath,
                    Parameters = ParseParametersFromJson(parametersJson),
                    RequiresAuth = true,
                    Response = new FunctionResponse
                    {
                        ExpectedFormat = "application/json"
                    }
                };
                
                functions.Add(function);
            }
        }
        
        return functions;
    }
    
    private string GenerateFunctionName(string apiPath, string method)
    {
        // Convert /api/customers -> getCustomers
        // Convert /api/customers/{id} -> getCustomerById
        
        var parts = apiPath.Trim('/').Split('/');
        var resource = parts.LastOrDefault(p => !p.Contains("{")) ?? "resource";
        
        var verb = method.ToLower() switch
        {
            "get" => "get",
            "post" => "create",
            "put" => "update",
            "patch" => "patch",
            "delete" => "delete",
            _ => "call"
        };
        
        // Capitalize first letter
        resource = char.ToUpper(resource[0]) + resource.Substring(1);
        
        var functionName = $"{verb}{resource}";
        
        // If path has parameters, add "ById" or similar
        if (apiPath.Contains("{"))
        {
            functionName += "ById";
        }
        
        return functionName;
    }
    
    private List<FunctionParameter> ParseParametersFromJson(string? parametersJson)
    {
        if (string.IsNullOrEmpty(parametersJson))
            return new List<FunctionParameter>();
        
        try
        {
            var oldParams = JsonSerializer.Deserialize<Dictionary<string, object>>(parametersJson);
            var newParams = new List<FunctionParameter>();
            
            foreach (var param in oldParams ?? new Dictionary<string, object>())
            {
                newParams.Add(new FunctionParameter
                {
                    Name = param.Key,
                    Type = GuessParameterType(param.Value),
                    Location = "query", // Default to query
                    Required = false,
                    Description = $"Migrated parameter: {param.Key}"
                });
            }
            
            return newParams;
        }
        catch
        {
            return new List<FunctionParameter>();
        }
    }
    
    private string GuessParameterType(object value)
    {
        return value switch
        {
            int => "number",
            long => "number",
            double => "number",
            bool => "boolean",
            DateTime => "string",
            _ => "string"
        };
    }
    
    private async Task UpdateDataSourceWithFunctionsAsync(
        NpgsqlConnection connection,
        Guid dataSourceId,
        List<FunctionDefinition> functions)
    {
        var functionsJson = JsonSerializer.Serialize(functions, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        var sql = @"
            UPDATE data_sources
            SET functions = @functions::jsonb,
                updated_at = @updatedAt,
                updated_by = 'DataMigration'
            WHERE id = @id
        ";
        
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("functions", functionsJson);
        cmd.Parameters.AddWithValue("updatedAt", DateTime.UtcNow);
        cmd.Parameters.AddWithValue("id", dataSourceId);
        
        await cmd.ExecuteNonQueryAsync();
    }
    
    public async Task MigratePipelinesAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        
        // Get all pipelines
        var pipelines = await GetAllPipelinesAsync(connection);
        
        foreach (var pipeline in pipelines)
        {
            // Get DataSource functions
            var dataSourceFunctions = await GetDataSourceFunctionsAsync(
                connection,
                pipeline.DataSourceId
            );
            
            // Find matching function
            var matchingFunction = FindMatchingFunction(
                dataSourceFunctions,
                pipeline.ApiPath,
                pipeline.Method
            );
            
            if (matchingFunction != null)
            {
                // Update pipeline with function ID
                await UpdatePipelineWithFunctionIdAsync(
                    connection,
                    pipeline.Id,
                    matchingFunction.Id,
                    pipeline.ParametersOld
                );
                
                Console.WriteLine($"Migrated Pipeline: {pipeline.Name} -> Function: {matchingFunction.Name}");
            }
            else
            {
                Console.WriteLine($"WARNING: No matching function found for pipeline: {pipeline.Name}");
            }
        }
    }
    
    private async Task<List<PipelineInfo>> GetAllPipelinesAsync(
        NpgsqlConnection connection)
    {
        var pipelines = new List<PipelineInfo>();
        
        var sql = @"
            SELECT id, name, data_source_id, api_path, method, parameters_old
            FROM pipelines
            WHERE NOT is_deleted
            AND function_id IS NULL
        ";
        
        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            pipelines.Add(new PipelineInfo
            {
                Id = reader.GetGuid(0),
                Name = reader.GetString(1),
                DataSourceId = reader.GetGuid(2),
                ApiPath = reader.GetString(3),
                Method = reader.GetString(4),
                ParametersOld = reader.IsDBNull(5) ? null : reader.GetString(5)
            });
        }
        
        return pipelines;
    }
    
    private async Task<List<FunctionDefinition>> GetDataSourceFunctionsAsync(
        NpgsqlConnection connection,
        Guid dataSourceId)
    {
        var sql = @"
            SELECT functions
            FROM data_sources
            WHERE id = @dataSourceId
        ";
        
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("dataSourceId", dataSourceId);
        
        var functionsJson = await cmd.ExecuteScalarAsync() as string;
        
        if (string.IsNullOrEmpty(functionsJson))
            return new List<FunctionDefinition>();
        
        return JsonSerializer.Deserialize<List<FunctionDefinition>>(functionsJson)
            ?? new List<FunctionDefinition>();
    }
    
    private FunctionDefinition? FindMatchingFunction(
        List<FunctionDefinition> functions,
        string apiPath,
        string method)
    {
        return functions.FirstOrDefault(f =>
            f.Path.Equals(apiPath, StringComparison.OrdinalIgnoreCase) &&
            f.Method.Equals(method, StringComparison.OrdinalIgnoreCase)
        );
    }
    
    private async Task UpdatePipelineWithFunctionIdAsync(
        NpgsqlConnection connection,
        Guid pipelineId,
        string functionId,
        string? oldParameters)
    {
        // Convert old parameters to static parameters
        var staticParameters = oldParameters != null
            ? JsonSerializer.Deserialize<Dictionary<string, object>>(oldParameters)
            : new Dictionary<string, object>();
        
        var staticParamsJson = JsonSerializer.Serialize(staticParameters);
        
        var sql = @"
            UPDATE pipelines
            SET function_id = @functionId,
                static_parameters = @staticParameters::jsonb,
                parameter_mappings = '{}'::jsonb,
                updated_at = @updatedAt,
                updated_by = 'DataMigration'
            WHERE id = @id
        ";
        
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("functionId", functionId);
        cmd.Parameters.AddWithValue("staticParameters", staticParamsJson);
        cmd.Parameters.AddWithValue("updatedAt", DateTime.UtcNow);
        cmd.Parameters.AddWithValue("id", pipelineId);
        
        await cmd.ExecuteNonQueryAsync();
    }
}

// Supporting classes
public class DataSourceInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? BaseUrl { get; set; }
    public string? OldConfig { get; set; }
    public string? AuthType { get; set; }
    public string? AuthConfig { get; set; }
}

public class PipelineInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid DataSourceId { get; set; }
    public string ApiPath { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string? ParametersOld { get; set; }
}
```

---

## 🔧 Manual Migration Steps

### For Production Systems

1. **Backup Database**
   ```bash
   pg_dump datacollector_shared > backup_before_migration.sql
   pg_dump datacollector_tenants > backup_tenants_before_migration.sql
   ```

2. **Run Schema Updates**
   ```bash
   dotnet ef migrations add AddDataSourceFunctions --context TenantDbContext
   dotnet ef database update --context TenantDbContext
   ```

3. **Run Data Migration**
   ```bash
   dotnet run --project MigrationTool migrate-datasources
   dotnet run --project MigrationTool migrate-pipelines
   ```

4. **Verify Migration**
   ```sql
   -- Check all DataSources have functions
   SELECT id, name, 
          jsonb_array_length(functions) as function_count
   FROM data_sources
   WHERE NOT is_deleted;
   
   -- Check all Pipelines have function_id
   SELECT id, name, function_id
   FROM pipelines
   WHERE NOT is_deleted
   AND function_id IS NULL;
   ```

5. **Update Application Code**
   - Deploy new service implementations
   - Update controllers
   - Update DTOs

6. **Test End-to-End**
   - Create new DataSource manually
   - Create new Collector referencing functions
   - Execute pipeline
   - Verify old collectors still work

---

## 🧪 Testing Migration

### Test Script

```csharp
public class MigrationValidationTests
{
    [Fact]
    public async Task ValidateDataSourceMigration()
    {
        // Arrange
        var dataSourceService = GetDataSourceService();
        
        // Act
        var dataSources = await dataSourceService.GetAllAsync(tenantId);
        
        // Assert
        foreach (var ds in dataSources)
        {
            var detail = await dataSourceService.GetByIdAsync(tenantId, ds.Id);
            
            // Verify functions exist
            Assert.NotNull(detail.Functions);
            Assert.NotEmpty(detail.Functions);
            
            // Verify each function has required fields
            foreach (var func in detail.Functions)
            {
                Assert.False(string.IsNullOrEmpty(func.Id));
                Assert.False(string.IsNullOrEmpty(func.Name));
                Assert.False(string.IsNullOrEmpty(func.Method));
                Assert.False(string.IsNullOrEmpty(func.Path));
            }
        }
    }
    
    [Fact]
    public async Task ValidatePipelineMigration()
    {
        // Arrange
        var collectorService = GetCollectorService();
        
        // Act
        var collectors = await collectorService.GetAllAsync(tenantId);
        
        // Assert
        foreach (var collector in collectors)
        {
            var detail = await collectorService.GetByIdAsync(tenantId, collector.Id);
            
            // Verify each pipeline has function reference
            foreach (var pipeline in detail.Pipelines)
            {
                Assert.False(string.IsNullOrEmpty(pipeline.FunctionId));
                
                // Verify function exists in DataSource
                var dataSource = await dataSourceService.GetByIdAsync(
                    tenantId, 
                    detail.DataSourceId.Value
                );
                
                var function = dataSource.Functions
                    .FirstOrDefault(f => f.Id == pipeline.FunctionId);
                
                Assert.NotNull(function);
            }
        }
    }
    
    [Fact]
    public async Task ValidatePipelineExecution()
    {
        // Test that migrated pipelines can still execute
        var result = await collectorService.ExecuteAsync(
            tenantId,
            collectorId,
            new ExecuteCollectorRequest(pipelineId, null, false)
        );
        
        Assert.True(result.Success);
    }
}
```

---

## 📋 Rollback Plan

If migration fails:

1. **Restore from Backup**
   ```bash
   psql datacollector_shared < backup_before_migration.sql
   psql datacollector_tenants < backup_tenants_before_migration.sql
   ```

2. **Revert Code Changes**
   ```bash
   git revert <migration-commit-hash>
   ```

3. **Revert Database Schema**
   ```bash
   dotnet ef migrations remove --context TenantDbContext
   dotnet ef database update --context TenantDbContext
   ```

---

## ✅ Post-Migration Checklist

- [ ] All DataSources have at least one function
- [ ] All Pipelines have function_id populated
- [ ] All function IDs in pipelines match DataSource functions
- [ ] Old columns (parameters_old, config_old) can be dropped
- [ ] Application works with new structure
- [ ] Existing collectors can execute successfully
- [ ] New collectors can be created with function references
- [ ] Documentation updated
- [ ] Team trained on new architecture

---

## 🎓 Training for Team

### Key Concepts to Understand

1. **DataSource = API Configuration**
   - Stores everything about the API
   - Functions define what operations are available
   
2. **Pipeline = Execution Flow**
   - References a function by ID
   - Adds parameter mappings and processing steps
   
3. **Creating New DataSource**
   - Manual: Define functions yourself
   - Generated: Import from Swagger/GraphQL/WSDL
   
4. **Creating New Collector**
   - Choose ONE DataSource
   - Add multiple pipelines using different functions
   - Each pipeline processes data differently

---

## 📞 Support

If you encounter issues during migration:

1. Check the migration logs
2. Validate data with test scripts
3. Review error messages
4. Consult the architecture guide
5. Contact the development team

---

**End of Migration Guide**
