using System.Text;
using System.Text.Json;
using DataCollector.Domain.Entities;

namespace DataCollector.Application.Services.Parsers;

/// <summary>
/// Comprehensive GraphQL introspection parser
/// </summary>
public class GraphQLParser
{
    private readonly IHttpClientFactory _httpClientFactory;

    public GraphQLParser(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<GraphQLParseResult> ParseAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        try
        {
            // Send introspection query
            var schema = await IntrospectSchemaAsync(endpoint, cancellationToken);

            var result = new GraphQLParseResult
            {
                Endpoint = endpoint,
                Functions = ParseSchema(schema),
                QueryType = GetQueryTypeName(schema),
                MutationType = GetMutationTypeName(schema),
                SubscriptionType = GetSubscriptionTypeName(schema),
                Types = ParseTypes(schema)
            };

            return result;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to parse GraphQL schema: {ex.Message}", ex);
        }
    }

    private async Task<JsonDocument> IntrospectSchemaAsync(string endpoint, CancellationToken cancellationToken)
    {
        var introspectionQuery = @"{
            ""query"": ""query IntrospectionQuery {
                __schema {
                    queryType { name }
                    mutationType { name }
                    subscriptionType { name }
                    types {
                        ...FullType
                    }
                    directives {
                        name
                        description
                        locations
                        args {
                            ...InputValue
                        }
                    }
                }
            }
            
            fragment FullType on __Type {
                kind
                name
                description
                fields(includeDeprecated: true) {
                    name
                    description
                    args {
                        ...InputValue
                    }
                    type {
                        ...TypeRef
                    }
                    isDeprecated
                    deprecationReason
                }
                inputFields {
                    ...InputValue
                }
                interfaces {
                    ...TypeRef
                }
                enumValues(includeDeprecated: true) {
                    name
                    description
                    isDeprecated
                    deprecationReason
                }
                possibleTypes {
                    ...TypeRef
                }
            }
            
            fragment InputValue on __InputValue {
                name
                description
                type { ...TypeRef }
                defaultValue
            }
            
            fragment TypeRef on __Type {
                kind
                name
                ofType {
                    kind
                    name
                    ofType {
                        kind
                        name
                        ofType {
                            kind
                            name
                            ofType {
                                kind
                                name
                                ofType {
                                    kind
                                    name
                                    ofType {
                                        kind
                                        name
                                        ofType {
                                            kind
                                            name
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }""
        }";

        var httpClient = _httpClientFactory.CreateClient();
        var request = new StringContent(introspectionQuery, Encoding.UTF8, "application/json");
        
        var response = await httpClient.PostAsync(endpoint, request, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"GraphQL introspection failed with status {response.StatusCode}");
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonDocument.Parse(content);
    }

    private List<FunctionDefinition> ParseSchema(JsonDocument schema)
    {
        var functions = new List<FunctionDefinition>();

        try
        {
            var data = schema.RootElement.GetProperty("data");
            var schemaObj = data.GetProperty("__schema");

            // Get all types
            var types = new Dictionary<string, JsonElement>();
            if (schemaObj.TryGetProperty("types", out var typesArray))
            {
                foreach (var type in typesArray.EnumerateArray())
                {
                    if (type.TryGetProperty("name", out var typeName))
                    {
                        var name = typeName.GetString();
                        if (!string.IsNullOrEmpty(name))
                        {
                            types[name] = type;
                        }
                    }
                }
            }

            // Parse queries
            if (schemaObj.TryGetProperty("queryType", out var queryType) &&
                queryType.TryGetProperty("name", out var queryTypeName))
            {
                var queryTypeNameStr = queryTypeName.GetString();
                if (!string.IsNullOrEmpty(queryTypeNameStr) && types.TryGetValue(queryTypeNameStr, out var queryTypeObj))
                {
                    if (queryTypeObj.TryGetProperty("fields", out var queryFields))
                    {
                        foreach (var field in queryFields.EnumerateArray())
                        {
                            var function = ParseField(field, "query");
                            if (function != null)
                            {
                                functions.Add(function);
                            }
                        }
                    }
                }
            }

            // Parse mutations
            if (schemaObj.TryGetProperty("mutationType", out var mutationType) &&
                mutationType.TryGetProperty("name", out var mutationTypeName))
            {
                var mutationTypeNameStr = mutationTypeName.GetString();
                if (!string.IsNullOrEmpty(mutationTypeNameStr) && types.TryGetValue(mutationTypeNameStr, out var mutationTypeObj))
                {
                    if (mutationTypeObj.TryGetProperty("fields", out var mutationFields))
                    {
                        foreach (var field in mutationFields.EnumerateArray())
                        {
                            var function = ParseField(field, "mutation");
                            if (function != null)
                            {
                                functions.Add(function);
                            }
                        }
                    }
                }
            }

            // Parse subscriptions
            if (schemaObj.TryGetProperty("subscriptionType", out var subscriptionType) &&
                subscriptionType.TryGetProperty("name", out var subscriptionTypeName))
            {
                var subscriptionTypeNameStr = subscriptionTypeName.GetString();
                if (!string.IsNullOrEmpty(subscriptionTypeNameStr) && types.TryGetValue(subscriptionTypeNameStr, out var subscriptionTypeObj))
                {
                    if (subscriptionTypeObj.TryGetProperty("fields", out var subscriptionFields))
                    {
                        foreach (var field in subscriptionFields.EnumerateArray())
                        {
                            var function = ParseField(field, "subscription");
                            if (function != null)
                            {
                                functions.Add(function);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to parse GraphQL schema fields: {ex.Message}", ex);
        }

        return functions;
    }

    private FunctionDefinition? ParseField(JsonElement field, string operationType)
    {
        try
        {
            var name = field.GetProperty("name").GetString() ?? "";
            var description = field.TryGetProperty("description", out var desc) ? desc.GetString() : null;
            var isDeprecated = field.TryGetProperty("isDeprecated", out var deprecated) && deprecated.GetBoolean();
            var deprecationReason = field.TryGetProperty("deprecationReason", out var reason) ? reason.GetString() : null;

            var parameters = new List<FunctionParameter>();
            if (field.TryGetProperty("args", out var args))
            {
                foreach (var arg in args.EnumerateArray())
                {
                    var param = ParseArgument(arg);
                    if (param != null)
                    {
                        parameters.Add(param);
                    }
                }
            }

            var returnType = "";
            if (field.TryGetProperty("type", out var type))
            {
                returnType = GetTypeName(type);
            }

            var function = new FunctionDefinition
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Description = description ?? $"GraphQL {operationType}: {name}",
                Method = "POST",
                Path = "/graphql",
                Parameters = parameters,
                RequiresAuth = true,
                IsDeprecated = isDeprecated,
                DeprecationMessage = deprecationReason,
                ProtocolSpecific = new Dictionary<string, object>
                {
                    ["operationType"] = operationType,
                    ["fieldName"] = name,
                    ["returnType"] = returnType
                }
            };

            return function;
        }
        catch
        {
            return null;
        }
    }

    private FunctionParameter? ParseArgument(JsonElement arg)
    {
        try
        {
            var name = arg.GetProperty("name").GetString() ?? "";
            var description = arg.TryGetProperty("description", out var desc) ? desc.GetString() : null;
            var defaultValue = arg.TryGetProperty("defaultValue", out var def) ? def.GetString() : null;

            string type = "string";
            bool required = false;

            if (arg.TryGetProperty("type", out var typeObj))
            {
                type = GetTypeName(typeObj);
                required = IsNonNullType(typeObj);
            }

            return new FunctionParameter
            {
                Name = name,
                Type = type,
                Location = "body",
                Required = required,
                Description = description,
                Default = defaultValue
            };
        }
        catch
        {
            return null;
        }
    }

    private string GetTypeName(JsonElement type)
    {
        try
        {
            if (type.TryGetProperty("kind", out var kind))
            {
                var kindStr = kind.GetString();

                switch (kindStr)
                {
                    case "NON_NULL":
                        if (type.TryGetProperty("ofType", out var ofType))
                        {
                            return GetTypeName(ofType);
                        }
                        break;

                    case "LIST":
                        if (type.TryGetProperty("ofType", out var listType))
                        {
                            return $"[{GetTypeName(listType)}]";
                        }
                        break;

                    case "SCALAR":
                    case "OBJECT":
                    case "INTERFACE":
                    case "UNION":
                    case "ENUM":
                    case "INPUT_OBJECT":
                        if (type.TryGetProperty("name", out var name))
                        {
                            return name.GetString() ?? "Unknown";
                        }
                        break;
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return "Unknown";
    }

    private bool IsNonNullType(JsonElement type)
    {
        try
        {
            if (type.TryGetProperty("kind", out var kind))
            {
                return kind.GetString() == "NON_NULL";
            }
        }
        catch
        {
            // Ignore errors
        }

        return false;
    }

    private string? GetQueryTypeName(JsonDocument schema)
    {
        try
        {
            return schema.RootElement
                .GetProperty("data")
                .GetProperty("__schema")
                .GetProperty("queryType")
                .GetProperty("name")
                .GetString();
        }
        catch
        {
            return null;
        }
    }

    private string? GetMutationTypeName(JsonDocument schema)
    {
        try
        {
            return schema.RootElement
                .GetProperty("data")
                .GetProperty("__schema")
                .GetProperty("mutationType")
                .GetProperty("name")
                .GetString();
        }
        catch
        {
            return null;
        }
    }

    private string? GetSubscriptionTypeName(JsonDocument schema)
    {
        try
        {
            return schema.RootElement
                .GetProperty("data")
                .GetProperty("__schema")
                .GetProperty("subscriptionType")
                .GetProperty("name")
                .GetString();
        }
        catch
        {
            return null;
        }
    }

    private Dictionary<string, GraphQLType> ParseTypes(JsonDocument schema)
    {
        var types = new Dictionary<string, GraphQLType>();

        try
        {
            var schemaObj = schema.RootElement.GetProperty("data").GetProperty("__schema");

            if (schemaObj.TryGetProperty("types", out var typesArray))
            {
                foreach (var type in typesArray.EnumerateArray())
                {
                    if (type.TryGetProperty("name", out var typeName))
                    {
                        var name = typeName.GetString();
                        if (!string.IsNullOrEmpty(name) && !name.StartsWith("__"))
                        {
                            var graphqlType = new GraphQLType
                            {
                                Name = name,
                                Kind = type.TryGetProperty("kind", out var kind) ? kind.GetString() ?? "" : "",
                                Description = type.TryGetProperty("description", out var desc) ? desc.GetString() : null
                            };

                            types[name] = graphqlType;
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return types;
    }
}

public class GraphQLParseResult
{
    public string Endpoint { get; set; } = "";
    public List<FunctionDefinition> Functions { get; set; } = new();
    public string? QueryType { get; set; }
    public string? MutationType { get; set; }
    public string? SubscriptionType { get; set; }
    public Dictionary<string, GraphQLType> Types { get; set; } = new();
}

public class GraphQLType
{
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
    public string? Description { get; set; }
}