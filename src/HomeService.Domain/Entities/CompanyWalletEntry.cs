using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class CompanyWalletEntry : AuditableEntity
{
    private CompanyWalletEntry()
    {
    }

    public CompanyWalletEntry(
        Guid companyId,
        Guid walletId,
        CompanyWalletEntryType type,
        int amount,
        string idempotencyKey,
        string description,
        DateTimeOffset? eligibleAt = null,
        Guid? missionId = null,
        Guid? payoutRequestId = null,
        string currency = "XOF")
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        CompanyId = companyId;
        WalletId = walletId;
        Type = type;
        Amount = amount;
        IdempotencyKey = CleanRequired(idempotencyKey);
        Description = CleanRequired(description);
        EligibleAt = eligibleAt;
        MissionId = missionId;
        PayoutRequestId = payoutRequestId;
        Currency = string.IsNullOrWhiteSpace(currency) ? "XOF" : currency.Trim().ToUpperInvariant();
    }

    public Guid CompanyId { get; private set; }
    public Guid WalletId { get; private set; }
    public CompanyWallet? Wallet { get; private set; }
    public CompanyWalletEntryType Type { get; private set; }
    public int Amount { get; private set; }
    public string Currency { get; private set; } = "XOF";
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DateTimeOffset? EligibleAt { get; private set; }
    public Guid? MissionId { get; private set; }
    public Guid? PayoutRequestId { get; private set; }

    private static string CleanRequired(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("La valeur est obligatoire.", nameof(value));
        }

        return value.Trim();
    }
}
