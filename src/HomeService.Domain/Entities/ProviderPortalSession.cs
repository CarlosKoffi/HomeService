using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class ProviderPortalSession : AuditableEntity
{
    private ProviderPortalSession()
    {
    }

    public ProviderPortalSession(Guid providerId, string tokenHash, DateTimeOffset expiresAt)
    {
        ProviderId = providerId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    public Guid ProviderId { get; private set; }
    public ProviderProfile? Provider { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public void Revoke()
    {
        RevokedAt = DateTimeOffset.UtcNow;
        Touch();
    }
}
