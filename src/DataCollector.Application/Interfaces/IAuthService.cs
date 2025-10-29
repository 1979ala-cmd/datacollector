
using DataCollector.Domain.DTOs;
using System.Threading;
using System.Threading.Tasks;

namespace DataCollector.Application.Interfaces;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
    Task<bool> RevokeTokenAsync(string token, CancellationToken cancellationToken = default);
}
