using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class CompanyPortalSession : AuditableEntity
{
    private CompanyPortalSession()
    {
    }

    public CompanyPortalSession(Guid companyPortalUserId, string tokenHash, DateTimeOffset expiresAt)
    {
        CompanyPortalUserId = companyPortalUserId;
        TokenHash = tokenHash.Trim();
        ExpiresAt = expiresAt;
    }

    public Guid CompanyPortalUserId { get; private set; }
    public CompanyPortalUser? CompanyPortalUser { get; private set; }
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
