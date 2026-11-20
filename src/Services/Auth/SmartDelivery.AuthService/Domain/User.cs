using SmartDelivery.Shared.Domain;
using SmartDelivery.Shared.Security;

namespace SmartDelivery.AuthService.Domain;

public class User : AggregateRoot<Guid>
{
    public string Username { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string Role { get; private set; } = Roles.Customer;
    public DateTime CreatedAtUtc { get; private set; }

    private User() { } // EF Core

    public User(Guid id, string username, string email, string passwordHash, string role)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        Username = username.Trim();
        Email = email.Trim().ToLowerInvariant();
        PasswordHash = passwordHash;
        Role = string.IsNullOrWhiteSpace(role) ? Roles.Customer : role;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void UpdatePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
    }

    public void UpdateRole(string newRole)
    {
        Role = newRole;
    }
}

public class RefreshToken : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public bool IsActive => !IsRevoked && DateTime.UtcNow < ExpiresAtUtc;

    private RefreshToken() { }

    public RefreshToken(Guid userId, string token, DateTime expiresAtUtc)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Token = token;
        ExpiresAtUtc = expiresAtUtc;
        IsRevoked = false;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void Revoke()
    {
        IsRevoked = true;
    }
}
