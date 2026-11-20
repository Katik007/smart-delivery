using SmartDelivery.Shared.Domain;

namespace SmartDelivery.AuthService.Application.DTOs;

public record RegisterRequest(string Username, string Email, string Password, string? Role);

public record LoginRequest(string UsernameOrEmail, string Password);

public record RefreshTokenRequest(string RefreshToken);

public record AuthResponse(
    Guid UserId,
    string Username,
    string Email,
    string Role,
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAtUtc
);

public record UserProfileResponse(
    Guid UserId,
    string Username,
    string Email,
    string Role,
    DateTime CreatedAtUtc
);

public interface IAuthService
{
    Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<Result<AuthResponse>> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task<Result<UserProfileResponse>> GetProfileAsync(Guid userId, CancellationToken ct = default);
}
