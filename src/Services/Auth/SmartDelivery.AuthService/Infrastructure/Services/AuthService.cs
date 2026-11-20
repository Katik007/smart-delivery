using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using SmartDelivery.AuthService.Application.DTOs;
using SmartDelivery.AuthService.Domain;
using SmartDelivery.AuthService.Infrastructure.Data;
using SmartDelivery.Shared.Domain;
using SmartDelivery.Shared.Security;

namespace SmartDelivery.AuthService.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly AuthDbContext _dbContext;
    private readonly JwtSettings _jwtSettings;
    private readonly IDistributedCache _cache;

    public AuthService(AuthDbContext dbContext, IOptions<JwtSettings> jwtOptions, IDistributedCache cache)
    {
        _dbContext = dbContext;
        _jwtSettings = jwtOptions.Value;
        _cache = cache;
    }

    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
            return Result<AuthResponse>.Failure("Username is required.");
        if (string.IsNullOrWhiteSpace(request.Email))
            return Result<AuthResponse>.Failure("Email is required.");
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            return Result<AuthResponse>.Failure("Password must be at least 6 characters.");

        var emailLower = request.Email.Trim().ToLowerInvariant();
        var usernameTrim = request.Username.Trim();

        var exists = await _dbContext.Users.AnyAsync(u => u.Email == emailLower || u.Username == usernameTrim, ct);
        if (exists)
            return Result<AuthResponse>.Failure("User with this email or username already exists.");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var role = string.IsNullOrWhiteSpace(request.Role) ? Roles.Customer : request.Role;

        var user = new User(Guid.NewGuid(), usernameTrim, emailLower, passwordHash, role);
        _dbContext.Users.Add(user);

        var tokenResponse = GenerateAuthResponse(user);
        await _dbContext.SaveChangesAsync(ct);

        return Result<AuthResponse>.Success(tokenResponse);
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.UsernameOrEmail) || string.IsNullOrWhiteSpace(request.Password))
            return Result<AuthResponse>.Failure("Username/Email and password are required.");

        var identifier = request.UsernameOrEmail.Trim().ToLowerInvariant();
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == identifier || u.Username.ToLower() == identifier, ct);
        if (user == null)
            return Result<AuthResponse>.Failure("Invalid credentials.");

        var isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        if (!isPasswordValid)
            return Result<AuthResponse>.Failure("Invalid credentials.");

        var tokenResponse = GenerateAuthResponse(user);
        await _dbContext.SaveChangesAsync(ct);

        return Result<AuthResponse>.Success(tokenResponse);
    }

    public async Task<Result<AuthResponse>> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Result<AuthResponse>.Failure("Refresh token is required.");

        var tokenEntity = await _dbContext.RefreshTokens.FirstOrDefaultAsync(r => r.Token == refreshToken, ct);
        if (tokenEntity == null || !tokenEntity.IsActive)
            return Result<AuthResponse>.Failure("Invalid or expired refresh token.");

        var user = await _dbContext.Users.FindAsync(new object[] { tokenEntity.UserId }, ct);
        if (user == null)
            return Result<AuthResponse>.Failure("User not found.");

        tokenEntity.Revoke();

        var tokenResponse = GenerateAuthResponse(user);
        await _dbContext.SaveChangesAsync(ct);

        return Result<AuthResponse>.Success(tokenResponse);
    }

    public async Task<Result<UserProfileResponse>> GetProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var cacheKey = $"user_profile_{userId}";
        try
        {
            var cached = await _cache.GetStringAsync(cacheKey, ct);
            if (!string.IsNullOrEmpty(cached))
            {
                var cachedProfile = System.Text.Json.JsonSerializer.Deserialize<UserProfileResponse>(cached);
                if (cachedProfile != null) return Result<UserProfileResponse>.Success(cachedProfile);
            }
        }
        catch
        {
            // Cache fallback
        }

        var user = await _dbContext.Users.FindAsync(new object[] { userId }, ct);
        if (user == null)
            return Result<UserProfileResponse>.Failure("User not found.");

        var profile = new UserProfileResponse(user.Id, user.Username, user.Email, user.Role, user.CreatedAtUtc);

        try
        {
            var serialized = System.Text.Json.JsonSerializer.Serialize(profile);
            await _cache.SetStringAsync(cacheKey, serialized, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            }, ct);
        }
        catch
        {
            // Cache failure ignored
        }

        return Result<UserProfileResponse>.Success(profile);
    }

    private AuthResponse GenerateAuthResponse(User user)
    {
        var accessToken = JwtTokenHelper.GenerateToken(user.Id, user.Username, user.Email, user.Role, _jwtSettings);
        var rawRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var expiresAtUtc = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays);

        var refreshTokenEntity = new RefreshToken(user.Id, rawRefreshToken, expiresAtUtc);
        _dbContext.RefreshTokens.Add(refreshTokenEntity);

        return new AuthResponse(
            user.Id,
            user.Username,
            user.Email,
            user.Role,
            accessToken,
            rawRefreshToken,
            DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes)
        );
    }
}
