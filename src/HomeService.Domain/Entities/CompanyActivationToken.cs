using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class CompanyActivationToken : AuditableEntity
{
    private CompanyActivationToken()
    {
    }

    internal CompanyActivationToken(Guid companyApplicationId, string tokenHash, DateTimeOffset expiresAt, string activationLink)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("Le hash du token d'activation est obligatoire.", nameof(tokenHash));
        }

        CompanyApplicationId = companyApplicationId;
        TokenHash = tokenHash.Trim();
        ExpiresAt = expiresAt;
        ActivationLink = activationLink.Trim();
    }

    public Guid CompanyApplicationId { get; private set; }
    public CompanyApplication? CompanyApplication { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public string ActivationLink { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? UsedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? RevocationReason { get; private set; }
    public bool IsActive => UsedAt is null && RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;

    public void MarkUsed()
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Le token d'activation n'est plus actif.");
        }

        UsedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    public void Revoke(string? reason = null)
    {
        if (UsedAt is not null || RevokedAt is not null)
        {
            return;
        }

        RevokedAt = DateTimeOffset.UtcNow;
        RevocationReason = reason?.Trim();
        Touch();
    }
}
