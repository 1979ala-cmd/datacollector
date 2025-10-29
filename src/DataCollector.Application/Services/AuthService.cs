using DataCollector.Domain.DTOs;
using DataCollector.Application.Interfaces;
using DataCollector.Domain.Entities;
using DataCollector.Domain.Exceptions;
using DataCollector.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace DataCollector.Application.Services;

public class AuthService : IAuthService
{
    private readonly SharedDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;

    public AuthService(
        SharedDbContext context,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles)
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Email == request.Email && !u.IsDeleted, cancellationToken);

        if (user == null || !user.IsActive)
            throw new DomainException("Invalid email or password");

        if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
            throw new DomainException("Invalid email or password");

        if (user.Tenant == null || !user.Tenant.IsActive)
            throw new DomainException("Tenant is not active");

        var roles = user.UserRoles.Select(ur => ur.Role).ToList();
        var accessToken = _tokenGenerator.GenerateAccessToken(user, roles);
        var refreshToken = _tokenGenerator.GenerateRefreshToken(user.Id);

        // Save refresh token
        _context.RefreshTokens.Add(refreshToken);

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync(cancellationToken);

        var userDto = new UserDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            roles
        );

        return new LoginResponse(
            accessToken,
            refreshToken.Token,
            3600, // 1 hour in seconds
            "Bearer",
            userDto
        );
    }

    public async Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email && !u.IsDeleted, cancellationToken);

        if (existingUser != null)
            throw new DomainException("User with this email already exists");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "System"
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        return new UserDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            new List<string>()
        );
    }

    public async Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var refreshToken = await _context.RefreshTokens
            .Include(rt => rt.User)
                .ThenInclude(u => u!.UserRoles)
            .Include(rt => rt.User!.Tenant)
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken && !rt.IsRevoked, cancellationToken);

        if (refreshToken == null || !refreshToken.IsActive)
            throw new DomainException("Invalid refresh token");

        var user = refreshToken.User!;

        if (!user.IsActive || user.Tenant == null || !user.Tenant.IsActive)
            throw new DomainException("User or tenant is not active");

        // Revoke old token
        refreshToken.IsRevoked = true;
        refreshToken.RevokedAt = DateTime.UtcNow;

        // Generate new tokens
        var roles = user.UserRoles.Select(ur => ur.Role).ToList();
        var newAccessToken = _tokenGenerator.GenerateAccessToken(user, roles);
        var newRefreshToken = _tokenGenerator.GenerateRefreshToken(user.Id);

        refreshToken.ReplacedByToken = newRefreshToken.Token;
        _context.RefreshTokens.Add(newRefreshToken);

        await _context.SaveChangesAsync(cancellationToken);

        var userDto = new UserDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            roles
        );

        return new LoginResponse(
            newAccessToken,
            newRefreshToken.Token,
            3600,
            "Bearer",
            userDto
        );
    }

    public async Task<bool> RevokeTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var refreshToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == token, cancellationToken);

        if (refreshToken == null)
            return false;

        refreshToken.IsRevoked = true;
        refreshToken.RevokedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
