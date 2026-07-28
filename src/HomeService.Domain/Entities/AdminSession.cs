using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class AdminSession : AuditableEntity
{
    private AdminSession()
    {
    }

    public AdminSession(Guid adminUserId, string tokenHash, DateTimeOffset expiresAt)
    {
        AdminUserId = adminUserId;
        TokenHash = tokenHash.Trim();
        ExpiresAt = expiresAt;
    }

    public Guid AdminUserId { get; private set; }
    public AdminUser? AdminUser { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public DateTimeOffset? LastSeenAt { get; private set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public void TouchSeen(DateTimeOffset seenAt)
    {
        LastSeenAt = seenAt;
        Touch();
    }

    public void Revoke(DateTimeOffset revokedAt)
    {
        RevokedAt = revokedAt;
        Touch();
    }
}
