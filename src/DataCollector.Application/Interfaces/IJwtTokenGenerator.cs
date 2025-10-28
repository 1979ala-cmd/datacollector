using DataCollector.Domain.Entities;

namespace DataCollector.Application.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateAccessToken(User user, IEnumerable<string> roles);
    RefreshToken GenerateRefreshToken(Guid userId);
    bool ValidateRefreshToken(string token);
}
