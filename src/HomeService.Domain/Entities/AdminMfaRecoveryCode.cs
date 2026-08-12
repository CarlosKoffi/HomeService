using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class AdminMfaRecoveryCode : AuditableEntity
{
    private AdminMfaRecoveryCode()
    {
    }

    public AdminMfaRecoveryCode(Guid adminUserId, string codeHash)
    {
        AdminUserId = adminUserId;
        CodeHash = codeHash;
    }

    public Guid AdminUserId { get; private set; }
    public AdminUser? AdminUser { get; private set; }
    public string CodeHash { get; private set; } = string.Empty;
    public DateTimeOffset? UsedAt { get; private set; }
    public bool IsUsed => UsedAt.HasValue;

    public void MarkUsed(DateTimeOffset usedAt)
    {
        if (UsedAt.HasValue)
        {
            throw new InvalidOperationException("Ce code de secours a deja ete utilise.");
        }

        UsedAt = usedAt;
        Touch();
    }
}
