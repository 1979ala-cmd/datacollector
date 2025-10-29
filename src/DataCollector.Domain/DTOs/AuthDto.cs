namespace DataCollector.Domain.DTOs;
public record LoginRequest(string Email, string Password);
public record LoginResponse(string AccessToken, string RefreshToken, int ExpiresIn, string TokenType, UserDto User);
public record RefreshTokenRequest(string RefreshToken);
public record RegisterRequest(string Email, string Password, string FirstName, string LastName);
public record UserDto(Guid Id, string Email, string FirstName, string LastName, IEnumerable<string> Roles);
