using DataCollector.Domain.Entities;
using System;
using System.Collections.Generic;

namespace DataCollector.Domain.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateAccessToken(User user, IEnumerable<string> roles);
    RefreshToken GenerateRefreshToken(Guid userId);
    bool ValidateRefreshToken(string token);
}
