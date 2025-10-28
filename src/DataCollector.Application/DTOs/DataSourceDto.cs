using DataCollector.Domain.Enums;
namespace DataCollector.Application.DTOs;
public record DataSourceDto(Guid Id, string Name, string Description, DataSourceProtocol Protocol, string Endpoint, bool IsActive);
public record CreateDataSourceRequest(string Name, string Description, string Type, string Protocol, string Endpoint, string AuthType, object? AuthConfig);
