using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class CompanyPayoutRequest : AuditableEntity
{
    private CompanyPayoutRequest()
    {
    }

    public CompanyPayoutRequest(
        Guid companyId,
        Guid destinationId,
        CompanyPayoutMethod method,
        CompanySettlementFrequency frequency,
        int grossAmount,
        int feeAmount,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        string currency = "XOF")
    {
        if (grossAmount <= 0 || feeAmount < 0 || feeAmount >= grossAmount)
        {
            throw new ArgumentOutOfRangeException(nameof(grossAmount), "Le montant net doit rester positif.");
        }

        CompanyId = companyId;
        DestinationId = destinationId;
        Method = method;
        Frequency = frequency;
        GrossAmount = grossAmount;
        FeeAmount = feeAmount;
        NetAmount = grossAmount - feeAmount;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        Currency = string.IsNullOrWhiteSpace(currency) ? "XOF" : currency.Trim().ToUpperInvariant();
        Reference = $"WPO-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..25].ToUpperInvariant();
    }

    public Guid CompanyId { get; private set; }
    public Guid DestinationId { get; private set; }
    public CompanyPayoutDestination? Destination { get; private set; }
    public CompanyPayoutMethod Method { get; private set; }
    public CompanySettlementFrequency Frequency { get; private set; }
    public CompanyPayoutStatus Status { get; private set; } = CompanyPayoutStatus.Submitted;
    public int GrossAmount { get; private set; }
    public int FeeAmount { get; private set; }
    public int NetAmount { get; private set; }
    public string Currency { get; private set; } = "XOF";
    public string Reference { get; private set; } = string.Empty;
    public DateTimeOffset PeriodStart { get; private set; }
    public DateTimeOffset PeriodEnd { get; private set; }
    public string? ExternalTransactionId { get; private set; }
    public string? FailureReason { get; private set; }
    public string? ProofReference { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public DateTimeOffset? ProcessingAt { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }

    public void Approve(DateTimeOffset? now = null)
    {
        if (Status != CompanyPayoutStatus.Submitted)
        {
            throw new InvalidOperationException("Seul un reversement soumis peut etre approuve.");
        }

        Status = CompanyPayoutStatus.Approved;
        ApprovedAt = now ?? DateTimeOffset.UtcNow;
        Touch();
    }

    public void MarkProcessing(string externalTransactionId, DateTimeOffset? now = null)
    {
        if (Status is not (CompanyPayoutStatus.Approved or CompanyPayoutStatus.Processing))
        {
            throw new InvalidOperationException("Le reversement n'est pas pret a etre traite.");
        }

        ExternalTransactionId = string.IsNullOrWhiteSpace(externalTransactionId)
            ? ExternalTransactionId
            : externalTransactionId.Trim();
        Status = CompanyPayoutStatus.Processing;
        ProcessingAt ??= now ?? DateTimeOffset.UtcNow;
        Touch();
    }

    public void MarkPaid(string? proofReference = null, DateTimeOffset? now = null)
    {
        if (Status is CompanyPayoutStatus.Paid or CompanyPayoutStatus.Rejected)
        {
            return;
        }

        Status = CompanyPayoutStatus.Paid;
        ProofReference = string.IsNullOrWhiteSpace(proofReference) ? ProofReference : proofReference.Trim();
        PaidAt = now ?? DateTimeOffset.UtcNow;
        FailureReason = null;
        Touch();
    }

    public void MarkFailed(string reason)
    {
        if (Status == CompanyPayoutStatus.Paid)
        {
            throw new InvalidOperationException("Un reversement paye ne peut pas echouer.");
        }

        Status = CompanyPayoutStatus.Failed;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? "Echec du reversement." : reason.Trim();
        Touch();
    }

    public void Reject(string reason)
    {
        if (Status != CompanyPayoutStatus.Submitted)
        {
            throw new InvalidOperationException("Seul un reversement soumis peut etre rejete.");
        }

        Status = CompanyPayoutStatus.Rejected;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? "Reversement rejete." : reason.Trim();
        Touch();
    }
}
