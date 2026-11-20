using SmartDelivery.Shared.Domain;

namespace SmartDelivery.AuthService.Domain;

public class UserAuditLog : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string IpAddress { get; private set; } = string.Empty;
    public DateTime OccurredAtUtc { get; private set; }
    public bool Success { get; private set; }
    public string? Details { get; private set; }

    private UserAuditLog() { }

    public UserAuditLog(Guid id, Guid userId, string action, string ipAddress, bool success, string? details = null)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        UserId = userId;
        Action = action;
        IpAddress = ipAddress;
        Success = success;
        Details = details;
        OccurredAtUtc = DateTime.UtcNow;
    }
}
