using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class CustomerSession : AuditableEntity
{
    private CustomerSession()
    {
    }

    public CustomerSession(Guid customerId, string tokenHash, DateTimeOffset expiresAt)
    {
        CustomerId = customerId;
        TokenHash = tokenHash.Trim();
        ExpiresAt = expiresAt;
    }

    public Guid CustomerId { get; private set; }
    public CustomerProfile? Customer { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public bool IsActive => RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;

    public void Revoke()
    {
        RevokedAt = DateTimeOffset.UtcNow;
        Touch();
    }
}
