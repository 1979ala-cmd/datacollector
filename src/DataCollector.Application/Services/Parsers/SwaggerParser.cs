using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DataCollector.Domain.Entities;

namespace DataCollector.Application.Services.Parsers;

/// <summary>
/// Comprehensive OpenAPI/Swagger parser supporting OpenAPI 3.x and Swagger 2.0
/// </summary>
public class SwaggerParser
{
    public SwaggerParseResult Parse(string content)
    {
        try
        {
            var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            // Determine version
            var isOpenApi3 = root.TryGetProperty("openapi", out var openApiVersion) && 
                           openApiVersion.GetString()?.StartsWith("3") == true;
            
            var isSwagger2 = root.TryGetProperty("swagger", out var swaggerVersion) && 
                           swaggerVersion.GetString()?.StartsWith("2") == true;

            if (!isOpenApi3 && !isSwagger2)
            {
                throw new System.Exception("Invalid Swagger/OpenAPI document. Must be OpenAPI 3.x or Swagger 2.0");
            }

            var result = new SwaggerParseResult
            {
                Title = GetTitle(root),
                Version = GetVersion(root),
                Description = GetDescription(root),
                BaseUrl = GetBaseUrl(root, isOpenApi3),
                Functions = ParsePaths(root, isOpenApi3),
                SecuritySchemes = ParseSecuritySchemes(root, isOpenApi3),
                Components = ParseComponents(root, isOpenApi3)
            };

            return result;
        }
        catch (System.Exception ex)
        {
            throw new System.Exception($"Failed to parse Swagger/OpenAPI document: {ex.Message}", ex);
        }
    }

    private string GetTitle(JsonElement root)
    {
        try
        {
            return root.GetProperty("info").GetProperty("title").GetString() ?? "API";
        }
        catch
        {
            return "API";
        }
    }

    private string GetVersion(JsonElement root)
    {
        try
        {
            return root.GetProperty("info").GetProperty("version").GetString() ?? "1.0.0";
        }
        catch
        {
            return "1.0.0";
        }
    }

    private string? GetDescription(JsonElement root)
    {
        try
        {
            return root.GetProperty("info").GetProperty("description").GetString();
        }
        catch
        {
            return null;
        }
    }

    private string? GetBaseUrl(JsonElement root, bool isOpenApi3)
    {
        try
        {
            if (isOpenApi3)
            {
                // OpenAPI 3.x: servers array
                if (root.TryGetProperty("servers", out var servers) && servers.GetArrayLength() > 0)
                {
                    var firstServer = servers[0];
                    if (firstServer.TryGetProperty("url", out var url))
                    {
                        return url.GetString();
                    }
                }
            }
            else
            {
                // Swagger 2.0: host + basePath
                var host = root.TryGetProperty("host", out var h) ? h.GetString() : null;
                if (host != null)
                {
                    var scheme = "https";
                    if (root.TryGetProperty("schemes", out var schemes) && schemes.GetArrayLength() > 0)
                    {
                        scheme = schemes[0].GetString() ?? "https";
                    }

                    var basePath = root.TryGetProperty("basePath", out var bp) ? bp.GetString() : "";
                    return $"{scheme}://{host}{basePath}";
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return null;
    }

    private List<FunctionDefinition> ParsePaths(JsonElement root, bool isOpenApi3)
    {
        var functions = new List<FunctionDefinition>();

        try
        {
            if (!root.TryGetProperty("paths", out var paths))
                return functions;

            foreach (var pathItem in paths.EnumerateObject())
            {
                var path = pathItem.Name;

                foreach (var operation in pathItem.Value.EnumerateObject())
                {
                    var method = operation.Name.ToUpper();
                    
                    // Skip non-HTTP methods
                    if (method == "PARAMETERS" || method == "SERVERS" || method == "$REF" || 
                        method == "SUMMARY" || method == "DESCRIPTION")
                        continue;

                    var function = ParseOperation(path, method, operation.Value, isOpenApi3);
                    if (function != null)
                    {
                        functions.Add(function);
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            throw new System.Exception($"Failed to parse paths: {ex.Message}", ex);
        }

        return functions;
    }

    private FunctionDefinition? ParseOperation(string path, string method, JsonElement operation, bool isOpenApi3)
    {
        try
        {
            var operationId = operation.TryGetProperty("operationId", out var opId)
                ? opId.GetString()
                : null;

            if (string.IsNullOrEmpty(operationId))
            {
                // Generate operation ID from method and path
                operationId = $"{method.ToLower()}{path.Replace("/", "_").Replace("{", "").Replace("}", "")}";
            }

            var summary = operation.TryGetProperty("summary", out var sum) ? sum.GetString() : null;
            var description = operation.TryGetProperty("description", out var desc) ? desc.GetString() : null;
            var deprecated = operation.TryGetProperty("deprecated", out var dep) && dep.GetBoolean();

            var function = new FunctionDefinition
            {
                Id = System.Guid.NewGuid().ToString(),
                Name = operationId ?? "",
                Description = summary ?? description ?? "",
                Method = method,
                Path = path,
                Parameters = ParseParameters(operation, path, isOpenApi3),
                RequestBody = ParseRequestBody(operation, isOpenApi3),
                Response = ParseResponses(operation, isOpenApi3),
                RequiresAuth = ParseSecurity(operation),
                IsDeprecated = deprecated,
                DeprecationMessage = deprecated ? "This operation is deprecated" : null,
                Scopes = ParseScopes(operation)
            };

            return function;
        }
        catch (System.Exception ex)
        {
            throw new System.Exception($"Failed to parse operation {method} {path}: {ex.Message}", ex);
        }
    }

    private List<FunctionParameter> ParseParameters(JsonElement operation, string path, bool isOpenApi3)
    {
        var parameters = new List<FunctionParameter>();

        try
        {
            // Parse explicit parameters
            if (operation.TryGetProperty("parameters", out var parametersArray))
            {
                foreach (var param in parametersArray.EnumerateArray())
                {
                    var parameter = ParseParameter(param, isOpenApi3);
                    if (parameter != null)
                    {
                        parameters.Add(parameter);
                    }
                }
            }

            // Extract path parameters from the path itself
            var pathParams = ExtractPathParameters(path);
            foreach (var pathParam in pathParams)
            {
                // Only add if not already in the list
                if (!parameters.Any(p => p.Name == pathParam && p.Location == "path"))
                {
                    parameters.Add(new FunctionParameter
                    {
                        Name = pathParam,
                        Type = "string",
                        Location = "path",
                        Required = true,
                        Description = $"Path parameter: {pathParam}"
                    });
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return parameters;
    }

    private FunctionParameter? ParseParameter(JsonElement param, bool isOpenApi3)
    {
        try
        {
            var name = param.GetProperty("name").GetString() ?? "";
            var location = param.GetProperty("in").GetString() ?? "query";
            var required = param.TryGetProperty("required", out var req) && req.GetBoolean();
            var description = param.TryGetProperty("description", out var desc) ? desc.GetString() : null;

            string type = "string";
            string? defaultValue = null;

            if (isOpenApi3)
            {
                // OpenAPI 3.x: schema object
                if (param.TryGetProperty("schema", out var schema))
                {
                    type = schema.TryGetProperty("type", out var t) ? t.GetString() ?? "string" : "string";
                    if (schema.TryGetProperty("default", out var def))
                    {
                        defaultValue = def.ToString();
                    }
                }
            }
            else
            {
                // Swagger 2.0: type directly on parameter
                type = param.TryGetProperty("type", out var t) ? t.GetString() ?? "string" : "string";
                if (param.TryGetProperty("default", out var def))
                {
                    defaultValue = def.ToString();
                }
            }

            return new FunctionParameter
            {
                Name = name,
                Type = type,
                Location = location,
                Required = required,
                Description = description,
                Default = defaultValue,
                Validation = ParseParameterValidation(param, isOpenApi3)
            };
        }
        catch
        {
            return null;
        }
    }

    private ValidationRules ParseParameterValidation(JsonElement param, bool isOpenApi3)
    {
        var validation = new ValidationRules();

        try
        {
            JsonElement schemaOrParam = param;
            if (isOpenApi3 && param.TryGetProperty("schema", out var schema))
            {
                schemaOrParam = schema;
            }

            if (schemaOrParam.TryGetProperty("pattern", out var pattern))
                validation.Pattern = pattern.GetString();

            if (schemaOrParam.TryGetProperty("minimum", out var min))
                validation.Min = min.GetInt32();

            if (schemaOrParam.TryGetProperty("maximum", out var max))
                validation.Max = max.GetInt32();

            if (schemaOrParam.TryGetProperty("minLength", out var minLen))
                validation.MinLength = minLen.GetInt32();

            if (schemaOrParam.TryGetProperty("maxLength", out var maxLen))
                validation.MaxLength = maxLen.GetInt32();

            if (schemaOrParam.TryGetProperty("enum", out var enumValues))
            {
                validation.AllowedValues = enumValues.EnumerateArray()
                    .Select(e => e.ToString())
                    .ToList();
            }
        }
        catch
        {
            // Ignore errors
        }

        return validation;
    }

    private List<string> ExtractPathParameters(string path)
    {
        var parameters = new List<string>();
        var parts = path.Split('/');

        foreach (var part in parts)
        {
            if (part.StartsWith("{") && part.EndsWith("}"))
            {
                var paramName = part.Trim('{', '}');
                parameters.Add(paramName);
            }
        }

        return parameters;
    }

    private FunctionRequestBody ParseRequestBody(JsonElement operation, bool isOpenApi3)
    {
        var requestBody = new FunctionRequestBody
        {
            ContentType = "application/json",
            Required = false,
            Schema = new Dictionary<string, object>()
        };

        try
        {
            if (isOpenApi3)
            {
                // OpenAPI 3.x: requestBody object
                if (operation.TryGetProperty("requestBody", out var body))
                {
                    requestBody.Required = body.TryGetProperty("required", out var req) && req.GetBoolean();

                    if (body.TryGetProperty("content", out var content))
                    {
                        // Try to get application/json first
                        if (content.TryGetProperty("application/json", out var jsonContent))
                        {
                            requestBody.ContentType = "application/json";
                            if (jsonContent.TryGetProperty("schema", out var schema))
                            {
                                requestBody.Schema = ParseSchema(schema);
                            }
                        }
                        else
                        {
                            // Get first content type
                            var firstContent = content.EnumerateObject().FirstOrDefault();
                            if (firstContent.Value.ValueKind != JsonValueKind.Undefined)
                            {
                                requestBody.ContentType = firstContent.Name;
                                if (firstContent.Value.TryGetProperty("schema", out var schema))
                                {
                                    requestBody.Schema = ParseSchema(schema);
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                // Swagger 2.0: body parameter
                if (operation.TryGetProperty("parameters", out var parameters))
                {
                    foreach (var param in parameters.EnumerateArray())
                    {
                        if (param.TryGetProperty("in", out var location) && location.GetString() == "body")
                        {
                            requestBody.Required = param.TryGetProperty("required", out var req) && req.GetBoolean();
                            
                            if (param.TryGetProperty("schema", out var schema))
                            {
                                requestBody.Schema = ParseSchema(schema);
                            }
                            break;
                        }
                    }
                }

                // Check for consumes
                if (operation.TryGetProperty("consumes", out var consumes) && consumes.GetArrayLength() > 0)
                {
                    requestBody.ContentType = consumes[0].GetString() ?? "application/json";
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return requestBody;
    }

    private FunctionResponse ParseResponses(JsonElement operation, bool isOpenApi3)
    {
        var response = new FunctionResponse
        {
            ExpectedFormat = "application/json",
            Schema = new Dictionary<string, object>(),
            StatusCodes = new Dictionary<string, ResponseStatusCode>()
        };

        try
        {
            if (!operation.TryGetProperty("responses", out var responses))
                return response;

            foreach (var resp in responses.EnumerateObject())
            {
                var statusCode = resp.Name;
                var statusDescription = "";
                Dictionary<string, object>? statusSchema = null;

                if (resp.Value.TryGetProperty("description", out var desc))
                {
                    statusDescription = desc.GetString() ?? "";
                }

                if (isOpenApi3)
                {
                    // OpenAPI 3.x: content object
                    if (resp.Value.TryGetProperty("content", out var content))
                    {
                        if (content.TryGetProperty("application/json", out var jsonContent))
                        {
                            if (jsonContent.TryGetProperty("schema", out var schema))
                            {
                                statusSchema = ParseSchema(schema);
                            }
                        }
                    }
                }
                else
                {
                    // Swagger 2.0: schema directly
                    if (resp.Value.TryGetProperty("schema", out var schema))
                    {
                        statusSchema = ParseSchema(schema);
                    }
                }

                response.StatusCodes[statusCode] = new ResponseStatusCode
                {
                    Description = statusDescription,
                    Schema = statusSchema
                };

                // Set expected format from first successful response
                if (statusCode.StartsWith("2") && statusSchema != null)
                {
                    response.Schema = statusSchema;
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return response;
    }

    private Dictionary<string, object> ParseSchema(JsonElement schema)
    {
        var result = new Dictionary<string, object>();

        try
        {
            if (schema.TryGetProperty("type", out var type))
            {
                result["type"] = type.GetString() ?? "object";
            }

            if (schema.TryGetProperty("properties", out var properties))
            {
                var props = new Dictionary<string, object>();
                foreach (var prop in properties.EnumerateObject())
                {
                    props[prop.Name] = ParseSchema(prop.Value);
                }
                result["properties"] = props;
            }

            if (schema.TryGetProperty("items", out var items))
            {
                result["items"] = ParseSchema(items);
            }

            if (schema.TryGetProperty("required", out var required))
            {
                result["required"] = required.EnumerateArray()
                    .Select(r => r.GetString() ?? "")
                    .ToList();
            }

            if (schema.TryGetProperty("$ref", out var refValue))
            {
                result["$ref"] = refValue.GetString() ?? "";
            }

            if (schema.TryGetProperty("description", out var desc))
            {
                result["description"] = desc.GetString() ?? "";
            }
        }
        catch
        {
            // Ignore errors
        }

        return result;
    }

    private bool ParseSecurity(JsonElement operation)
    {
        // If security is defined at operation level, it requires auth
        if (operation.TryGetProperty("security", out var security))
        {
            return security.GetArrayLength() > 0;
        }

        // Default to requiring auth
        return true;
    }

    private List<string> ParseScopes(JsonElement operation)
    {
        var scopes = new List<string>();

        try
        {
            if (operation.TryGetProperty("security", out var security))
            {
                foreach (var secItem in security.EnumerateArray())
                {
                    foreach (var scheme in secItem.EnumerateObject())
                    {
                        foreach (var scope in scheme.Value.EnumerateArray())
                        {
                            var scopeStr = scope.GetString();
                            if (!string.IsNullOrEmpty(scopeStr))
                            {
                                scopes.Add(scopeStr);
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return scopes;
    }

    private Dictionary<string, SecurityScheme> ParseSecuritySchemes(JsonElement root, bool isOpenApi3)
    {
        var schemes = new Dictionary<string, SecurityScheme>();

        try
        {
            JsonElement securitySchemes;

            if (isOpenApi3)
            {
                // OpenAPI 3.x: components.securitySchemes
                if (!root.TryGetProperty("components", out var components) ||
                    !components.TryGetProperty("securitySchemes", out securitySchemes))
                {
                    return schemes;
                }
            }
            else
            {
                // Swagger 2.0: securityDefinitions
                if (!root.TryGetProperty("securityDefinitions", out securitySchemes))
                {
                    return schemes;
                }
            }

            foreach (var scheme in securitySchemes.EnumerateObject())
            {
                var schemeName = scheme.Name;
                var schemeValue = scheme.Value;

                var securityScheme = new SecurityScheme
                {
                    Type = schemeValue.TryGetProperty("type", out var type) ? type.GetString() ?? "" : "",
                    Scheme = schemeValue.TryGetProperty("scheme", out var sch) ? sch.GetString() : null,
                    In = schemeValue.TryGetProperty("in", out var inProp) ? inProp.GetString() : null,
                    Name = schemeValue.TryGetProperty("name", out var name) ? name.GetString() : null,
                    BearerFormat = schemeValue.TryGetProperty("bearerFormat", out var bf) ? bf.GetString() : null
                };

                schemes[schemeName] = securityScheme;
            }
        }
        catch
        {
            // Ignore errors
        }

        return schemes;
    }

    private Dictionary<string, object> ParseComponents(JsonElement root, bool isOpenApi3)
    {
        var components = new Dictionary<string, object>();

        try
        {
            if (isOpenApi3 && root.TryGetProperty("components", out var comp))
            {
                if (comp.TryGetProperty("schemas", out var schemas))
                {
                    var schemaDict = new Dictionary<string, object>();
                    foreach (var schema in schemas.EnumerateObject())
                    {
                        schemaDict[schema.Name] = ParseSchema(schema.Value);
                    }
                    components["schemas"] = schemaDict;
                }
            }
            else if (!isOpenApi3 && root.TryGetProperty("definitions", out var definitions))
            {
                var schemaDict = new Dictionary<string, object>();
                foreach (var schema in definitions.EnumerateObject())
                {
                    schemaDict[schema.Name] = ParseSchema(schema.Value);
                }
                components["schemas"] = schemaDict;
            }
        }
        catch
        {
            // Ignore errors
        }

        return components;
    }
}

public class SwaggerParseResult
{
    public string Title { get; set; } = "";
    public string Version { get; set; } = "";
    public string? Description { get; set; }
    public string? BaseUrl { get; set; }
    public List<FunctionDefinition> Functions { get; set; } = new();
    public Dictionary<string, SecurityScheme> SecuritySchemes { get; set; } = new();
    public Dictionary<string, object> Components { get; set; } = new();
}

public class SecurityScheme
{
    public string Type { get; set; } = "";
    public string? Scheme { get; set; }
    public string? In { get; set; }
    public string? Name { get; set; }
    public string? BearerFormat { get; set; }
}