using System.Xml;
using System.Xml.Linq;
using DataCollector.Domain.Entities;

namespace DataCollector.Application.Services.Parsers;

/// <summary>
/// Comprehensive WSDL/SOAP parser with full XML support
/// </summary>
public class WsdlParser
{
    public WsdlParseResult Parse(string wsdlContent)
    {
        try
        {
            var doc = XDocument.Parse(wsdlContent);

            var result = new WsdlParseResult
            {
                ServiceName = GetServiceName(doc),
                TargetNamespace = GetTargetNamespace(doc),
                EndpointUrl = GetEndpointUrl(doc),
                Functions = ParseOperations(doc),
                Messages = ParseMessages(doc),
                PortTypes = ParsePortTypes(doc),
                Bindings = ParseBindings(doc)
            };

            return result;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to parse WSDL document: {ex.Message}", ex);
        }
    }

    private string GetServiceName(XDocument doc)
    {
        try
        {
            var ns = GetWsdlNamespace(doc);
            var service = doc.Descendants(ns + "service").FirstOrDefault();
            
            if (service != null)
            {
                var nameAttr = service.Attribute("name");
                if (nameAttr != null)
                {
                    return nameAttr.Value;
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return "SOAP Service";
    }

    private string? GetTargetNamespace(XDocument doc)
    {
        try
        {
            var ns = GetWsdlNamespace(doc);
            var definitions = doc.Element(ns + "definitions");
            
            if (definitions != null)
            {
                var targetNs = definitions.Attribute("targetNamespace");
                if (targetNs != null)
                {
                    return targetNs.Value;
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return null;
    }

    private string? GetEndpointUrl(XDocument doc)
    {
        try
        {
            var ns = GetWsdlNamespace(doc);
            var soapNs = XNamespace.Get("http://schemas.xmlsoap.org/wsdl/soap/");
            var soap12Ns = XNamespace.Get("http://schemas.xmlsoap.org/wsdl/soap12/");

            // Try SOAP 1.1
            var address = doc.Descendants(soapNs + "address").FirstOrDefault();
            if (address != null)
            {
                var locationAttr = address.Attribute("location");
                if (locationAttr != null)
                {
                    return locationAttr.Value;
                }
            }

            // Try SOAP 1.2
            address = doc.Descendants(soap12Ns + "address").FirstOrDefault();
            if (address != null)
            {
                var locationAttr = address.Attribute("location");
                if (locationAttr != null)
                {
                    return locationAttr.Value;
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return null;
    }

    private List<FunctionDefinition> ParseOperations(XDocument doc)
    {
        var functions = new List<FunctionDefinition>();

        try
        {
            var ns = GetWsdlNamespace(doc);
            var portTypes = doc.Descendants(ns + "portType");

            foreach (var portType in portTypes)
            {
                var operations = portType.Descendants(ns + "operation");

                foreach (var operation in operations)
                {
                    var function = ParseOperation(operation, doc);
                    if (function != null)
                    {
                        functions.Add(function);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to parse WSDL operations: {ex.Message}", ex);
        }

        return functions;
    }

    private FunctionDefinition? ParseOperation(XElement operation, XDocument doc)
    {
        try
        {
            var name = operation.Attribute("name")?.Value ?? "";
            
            var ns = GetWsdlNamespace(doc);
            var documentation = operation.Element(ns + "documentation")?.Value;

            // Get input and output messages
            var input = operation.Element(ns + "input");
            var output = operation.Element(ns + "output");

            var inputMessage = input?.Attribute("message")?.Value;
            var outputMessage = output?.Attribute("message")?.Value;

            // Parse parameters from input message
            var parameters = ParseMessageParameters(inputMessage, doc);

            // Get SOAP action
            var soapAction = GetSoapAction(name, doc);

            var function = new FunctionDefinition
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Description = documentation ?? $"SOAP operation: {name}",
                Method = "POST",
                Path = "/soap",
                Parameters = parameters,
                RequiresAuth = true,
                ProtocolSpecific = new Dictionary<string, object>
                {
                    ["soapAction"] = soapAction ?? name,
                    ["protocol"] = "SOAP",
                    ["inputMessage"] = inputMessage ?? "",
                    ["outputMessage"] = outputMessage ?? "",
                    ["namespace"] = GetTargetNamespace(doc) ?? ""
                },
                RequestBody = new FunctionRequestBody
                {
                    ContentType = "text/xml",
                    Required = true,
                    Schema = new Dictionary<string, object>
                    {
                        ["soapEnvelope"] = true
                    }
                },
                Response = new FunctionResponse
                {
                    ExpectedFormat = "text/xml",
                    Schema = new Dictionary<string, object>
                    {
                        ["soapEnvelope"] = true
                    }
                }
            };

            return function;
        }
        catch
        {
            return null;
        }
    }

    private List<FunctionParameter> ParseMessageParameters(string? messageName, XDocument doc)
    {
        var parameters = new List<FunctionParameter>();

        if (string.IsNullOrEmpty(messageName))
            return parameters;

        try
        {
            var ns = GetWsdlNamespace(doc);
            var xsdNs = XNamespace.Get("http://www.w3.org/2001/XMLSchema");

            // Remove namespace prefix if present
            var messageLocalName = messageName.Contains(":") 
                ? messageName.Split(':')[1] 
                : messageName;

            // Find the message definition
            var message = doc.Descendants(ns + "message")
                .FirstOrDefault(m => m.Attribute("name")?.Value == messageLocalName);

            if (message == null)
                return parameters;

            // Get parts
            var parts = message.Descendants(ns + "part");

            foreach (var part in parts)
            {
                var partName = part.Attribute("name")?.Value ?? "";
                var elementAttr = part.Attribute("element");
                var typeAttr = part.Attribute("type");

                string paramType = "string";

                if (elementAttr != null)
                {
                    // Element reference - try to resolve
                    var elementName = elementAttr.Value.Contains(":")
                        ? elementAttr.Value.Split(':')[1]
                        : elementAttr.Value;

                    paramType = elementName;
                }
                else if (typeAttr != null)
                {
                    // Direct type reference
                    var typeName = typeAttr.Value.Contains(":")
                        ? typeAttr.Value.Split(':')[1]
                        : typeAttr.Value;

                    paramType = MapXsdType(typeName);
                }

                parameters.Add(new FunctionParameter
                {
                    Name = partName,
                    Type = paramType,
                    Location = "body",
                    Required = true,
                    Description = $"SOAP parameter: {partName}"
                });
            }
        }
        catch
        {
            // Ignore errors
        }

        return parameters;
    }

    private string? GetSoapAction(string operationName, XDocument doc)
    {
        try
        {
            var ns = GetWsdlNamespace(doc);
            var soapNs = XNamespace.Get("http://schemas.xmlsoap.org/wsdl/soap/");
            var soap12Ns = XNamespace.Get("http://schemas.xmlsoap.org/wsdl/soap12/");

            // Find binding
            var bindings = doc.Descendants(ns + "binding");

            foreach (var binding in bindings)
            {
                var operations = binding.Descendants(ns + "operation");

                foreach (var operation in operations)
                {
                    var name = operation.Attribute("name")?.Value;
                    if (name == operationName)
                    {
                        // Try SOAP 1.1
                        var soapOp = operation.Element(soapNs + "operation");
                        if (soapOp != null)
                        {
                            var actionAttr = soapOp.Attribute("soapAction");
                            if (actionAttr != null)
                            {
                                return actionAttr.Value;
                            }
                        }

                        // Try SOAP 1.2
                        soapOp = operation.Element(soap12Ns + "operation");
                        if (soapOp != null)
                        {
                            var actionAttr = soapOp.Attribute("soapAction");
                            if (actionAttr != null)
                            {
                                return actionAttr.Value;
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

        return null;
    }

    private Dictionary<string, WsdlMessage> ParseMessages(XDocument doc)
    {
        var messages = new Dictionary<string, WsdlMessage>();

        try
        {
            var ns = GetWsdlNamespace(doc);
            var messageElements = doc.Descendants(ns + "message");

            foreach (var message in messageElements)
            {
                var name = message.Attribute("name")?.Value;
                if (!string.IsNullOrEmpty(name))
                {
                    var parts = new List<string>();
                    foreach (var part in message.Descendants(ns + "part"))
                    {
                        var partName = part.Attribute("name")?.Value;
                        if (!string.IsNullOrEmpty(partName))
                        {
                            parts.Add(partName);
                        }
                    }

                    messages[name] = new WsdlMessage
                    {
                        Name = name,
                        Parts = parts
                    };
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return messages;
    }

    private Dictionary<string, WsdlPortType> ParsePortTypes(XDocument doc)
    {
        var portTypes = new Dictionary<string, WsdlPortType>();

        try
        {
            var ns = GetWsdlNamespace(doc);
            var portTypeElements = doc.Descendants(ns + "portType");

            foreach (var portType in portTypeElements)
            {
                var name = portType.Attribute("name")?.Value;
                if (!string.IsNullOrEmpty(name))
                {
                    var operations = new List<string>();
                    foreach (var operation in portType.Descendants(ns + "operation"))
                    {
                        var opName = operation.Attribute("name")?.Value;
                        if (!string.IsNullOrEmpty(opName))
                        {
                            operations.Add(opName);
                        }
                    }

                    portTypes[name] = new WsdlPortType
                    {
                        Name = name,
                        Operations = operations
                    };
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return portTypes;
    }

    private Dictionary<string, WsdlBinding> ParseBindings(XDocument doc)
    {
        var bindings = new Dictionary<string, WsdlBinding>();

        try
        {
            var ns = GetWsdlNamespace(doc);
            var soapNs = XNamespace.Get("http://schemas.xmlsoap.org/wsdl/soap/");

            var bindingElements = doc.Descendants(ns + "binding");

            foreach (var binding in bindingElements)
            {
                var name = binding.Attribute("name")?.Value;
                var type = binding.Attribute("type")?.Value;
                
                if (!string.IsNullOrEmpty(name))
                {
                    var soapBinding = binding.Element(soapNs + "binding");
                    var transport = soapBinding?.Attribute("transport")?.Value;
                    var style = soapBinding?.Attribute("style")?.Value;

                    bindings[name] = new WsdlBinding
                    {
                        Name = name,
                        Type = type ?? "",
                        Transport = transport ?? "",
                        Style = style ?? ""
                    };
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return bindings;
    }

    private XNamespace GetWsdlNamespace(XDocument doc)
    {
        // Try to get WSDL namespace from root element
        var root = doc.Root;
        if (root != null)
        {
            var wsdlNs = root.GetNamespaceOfPrefix("wsdl");
            if (wsdlNs != null)
            {
                return wsdlNs;
            }

            // If no prefix, check if root is definitions
            if (root.Name.LocalName == "definitions")
            {
                return root.Name.Namespace;
            }
        }

        // Default WSDL namespace
        return XNamespace.Get("http://schemas.xmlsoap.org/wsdl/");
    }

    private string MapXsdType(string xsdType)
    {
        return xsdType.ToLower() switch
        {
            "string" => "string",
            "int" => "integer",
            "integer" => "integer",
            "long" => "integer",
            "short" => "integer",
            "byte" => "integer",
            "decimal" => "number",
            "float" => "number",
            "double" => "number",
            "boolean" => "boolean",
            "bool" => "boolean",
            "date" => "string",
            "datetime" => "string",
            "time" => "string",
            "duration" => "string",
            "base64binary" => "string",
            "hexbinary" => "string",
            "anyuri" => "string",
            _ => "string"
        };
    }
}

public class WsdlParseResult
{
    public string ServiceName { get; set; } = "";
    public string? TargetNamespace { get; set; }
    public string? EndpointUrl { get; set; }
    public List<FunctionDefinition> Functions { get; set; } = new();
    public Dictionary<string, WsdlMessage> Messages { get; set; } = new();
    public Dictionary<string, WsdlPortType> PortTypes { get; set; } = new();
    public Dictionary<string, WsdlBinding> Bindings { get; set; } = new();
}

public class WsdlMessage
{
    public string Name { get; set; } = "";
    public List<string> Parts { get; set; } = new();
}

public class WsdlPortType
{
    public string Name { get; set; } = "";
    public List<string> Operations { get; set; } = new();
}

public class WsdlBinding
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Transport { get; set; } = "";
    public string Style { get; set; } = "";
}