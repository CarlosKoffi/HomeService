using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class CompanyWallet : AuditableEntity
{
    private CompanyWallet()
    {
    }

    public CompanyWallet(Guid companyId, string currency = "XOF")
    {
        CompanyId = companyId;
        Currency = NormalizeCurrency(currency);
    }

    public Guid CompanyId { get; private set; }
    public Company? Company { get; private set; }
    public int PendingBalance { get; private set; }
    public int AvailableBalance { get; private set; }
    public int ReservedBalance { get; private set; }
    public int WithdrawnBalance { get; private set; }
    public string Currency { get; private set; } = "XOF";
    public long Version { get; private set; }

    public void CreditPending(int amount)
    {
        EnsurePositive(amount);
        PendingBalance = checked(PendingBalance + amount);
        AdvanceVersion();
    }

    public void MakeAvailable(int amount)
    {
        EnsurePositive(amount);
        if (PendingBalance < amount)
        {
            throw new InvalidOperationException("Le solde en attente est insuffisant.");
        }

        PendingBalance -= amount;
        AvailableBalance = checked(AvailableBalance + amount);
        AdvanceVersion();
    }

    public void Reserve(int amount)
    {
        EnsurePositive(amount);
        if (AvailableBalance < amount)
        {
            throw new InvalidOperationException("Le solde disponible est insuffisant.");
        }

        AvailableBalance -= amount;
        ReservedBalance = checked(ReservedBalance + amount);
        AdvanceVersion();
    }

    public void CompletePayout(int amount)
    {
        EnsurePositive(amount);
        if (ReservedBalance < amount)
        {
            throw new InvalidOperationException("Le solde reserve est insuffisant.");
        }

        ReservedBalance -= amount;
        WithdrawnBalance = checked(WithdrawnBalance + amount);
        AdvanceVersion();
    }

    public void ReleaseReservation(int amount)
    {
        EnsurePositive(amount);
        if (ReservedBalance < amount)
        {
            throw new InvalidOperationException("Le solde reserve est insuffisant.");
        }

        ReservedBalance -= amount;
        AvailableBalance = checked(AvailableBalance + amount);
        AdvanceVersion();
    }

    private void AdvanceVersion()
    {
        Version++;
        Touch();
    }

    private static void EnsurePositive(int amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Le montant doit etre strictement positif.");
        }
    }

    private static string NormalizeCurrency(string currency) =>
        string.IsNullOrWhiteSpace(currency) ? "XOF" : currency.Trim().ToUpperInvariant();
}
