using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class MissionAdditionalQuote : AuditableEntity
{
    private MissionAdditionalQuote()
    {
    }

    public MissionAdditionalQuote(
        Guid missionId,
        Guid providerId,
        Guid companyId,
        string reason,
        string? requestedPhotoStoragePath = null)
    {
        MissionId = missionId;
        ProviderId = providerId;
        CompanyId = companyId;
        Reason = RequireText(reason, "La raison du devis complementaire est obligatoire.");
        RequestedPhotoStoragePath = Clean(requestedPhotoStoragePath);
        RequestedAt = DateTimeOffset.UtcNow;
    }

    public Guid MissionId { get; private set; }
    public Mission? Mission { get; private set; }
    public Guid ProviderId { get; private set; }
    public ProviderProfile? Provider { get; private set; }
    public Guid CompanyId { get; private set; }
    public Company? Company { get; private set; }
    public MissionAdditionalQuoteStatus Status { get; private set; } = MissionAdditionalQuoteStatus.Requested;
    public string Reason { get; private set; } = string.Empty;
    public string? RequestedPhotoStoragePath { get; private set; }
    public int? Amount { get; private set; }
    public string Currency { get; private set; } = "XOF";
    public string? CompanyDescription { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }
    public DateTimeOffset? RejectedAt { get; private set; }
    public string? PaymentReference { get; private set; }

    public void Submit(int amount, string currency, string companyDescription)
    {
        if (Status != MissionAdditionalQuoteStatus.Requested)
        {
            throw new InvalidOperationException("Seul un devis complementaire demande peut etre envoye au client.");
        }

        if (amount <= 0)
        {
            throw new InvalidOperationException("Le montant du devis complementaire doit etre positif.");
        }

        Amount = amount;
        Currency = string.IsNullOrWhiteSpace(currency) ? "XOF" : currency.Trim().ToUpperInvariant();
        CompanyDescription = RequireText(companyDescription, "Le detail du devis complementaire est obligatoire.");
        SubmittedAt = DateTimeOffset.UtcNow;
        Status = MissionAdditionalQuoteStatus.Submitted;
        Touch();
    }

    public void MarkPaid(string? paymentReference)
    {
        if (Status != MissionAdditionalQuoteStatus.Submitted)
        {
            throw new InvalidOperationException("Seul un devis complementaire envoye peut etre paye.");
        }

        PaidAt = DateTimeOffset.UtcNow;
        PaymentReference = Clean(paymentReference);
        Status = MissionAdditionalQuoteStatus.Paid;
        Touch();
    }

    public void Reject()
    {
        if (Status != MissionAdditionalQuoteStatus.Submitted)
        {
            throw new InvalidOperationException("Seul un devis complementaire envoye peut etre refuse.");
        }

        RejectedAt = DateTimeOffset.UtcNow;
        Status = MissionAdditionalQuoteStatus.Rejected;
        Touch();
    }

    private static string RequireText(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(message);
        }

        return value.Trim();
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
