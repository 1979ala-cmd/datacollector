namespace DataCollector.Domain.Exceptions;
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
public class NotFoundException : DomainException
{
    public NotFoundException(string entityName, object key) : base($"{entityName} with key '{key}' not found.") { }
}
public class ValidationException : DomainException
{
    public Dictionary<string, string[]> Errors { get; }
    public ValidationException(Dictionary<string, string[]> errors) : base("Validation errors occurred.") { Errors = errors; }
}
