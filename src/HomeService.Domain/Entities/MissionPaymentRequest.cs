using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class MissionPaymentRequest : AuditableEntity
{
    private MissionPaymentRequest()
    {
    }

    public MissionPaymentRequest(
        Guid missionId,
        Guid customerId,
        Guid customerPaymentMethodId,
        string reference,
        string providerCode,
        int commercialAmount,
        int providerFeeAmount,
        int requestedAmount,
        string currency,
        DateTimeOffset expiresAt)
    {
        if (missionId == Guid.Empty || customerId == Guid.Empty || customerPaymentMethodId == Guid.Empty)
        {
            throw new ArgumentException("La mission, le client et le moyen de paiement sont obligatoires.");
        }

        MissionId = missionId;
        CustomerId = customerId;
        CustomerPaymentMethodId = customerPaymentMethodId;
        Reference = CleanRequired(reference, 120);
        ProviderCode = CleanRequired(providerCode, 40);
        CommercialAmount = Math.Max(0, commercialAmount);
        ProviderFeeAmount = Math.Max(0, providerFeeAmount);
        RequestedAmount = Math.Max(0, requestedAmount);
        Currency = CleanRequired(currency, 8).ToUpperInvariant();
        ExpiresAt = expiresAt;
    }

    public Guid MissionId { get; private set; }
    public Mission? Mission { get; private set; }
    public Guid CustomerId { get; private set; }
    public CustomerProfile? Customer { get; private set; }
    public Guid CustomerPaymentMethodId { get; private set; }
    public CustomerPaymentMethod? CustomerPaymentMethod { get; private set; }
    public string Reference { get; private set; } = string.Empty;
    public string? ExternalPaymentRequestId { get; private set; }
    public string? ExternalTransactionId { get; private set; }
    public string ProviderCode { get; private set; } = string.Empty;
    public int CommercialAmount { get; private set; }
    public int ProviderFeeAmount { get; private set; }
    public int RequestedAmount { get; private set; }
    public string Currency { get; private set; } = "XOF";
    public MissionPaymentRequestStatus Status { get; private set; } = MissionPaymentRequestStatus.Pending;
    public string? RedirectUrl { get; private set; }
    public string? FailureMessage { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public void AttachGatewayResponse(string? externalPaymentRequestId, string? redirectUrl, DateTimeOffset? expiresAt = null)
    {
        if (!string.IsNullOrWhiteSpace(externalPaymentRequestId))
        {
            ExternalPaymentRequestId = Clean(externalPaymentRequestId, 160);
        }

        if (!string.IsNullOrWhiteSpace(redirectUrl))
        {
            RedirectUrl = Clean(redirectUrl, 1000);
        }

        if (expiresAt.HasValue)
        {
            ExpiresAt = expiresAt.Value;
        }

        FailureMessage = null;
        Touch();
    }

    public void RecordPendingIssue(string? message)
    {
        FailureMessage = Clean(message, 500);
        Touch();
    }

    public void MarkSuccess(string? externalTransactionId = null)
    {
        Status = MissionPaymentRequestStatus.Success;
        ExternalTransactionId = Clean(externalTransactionId, 160) ?? ExternalTransactionId;
        FailureMessage = null;
        CompletedAt ??= DateTimeOffset.UtcNow;
        Touch();
    }

    public void MarkError(string? message, string? externalTransactionId = null)
    {
        if (Status == MissionPaymentRequestStatus.Success)
        {
            return;
        }

        Status = MissionPaymentRequestStatus.Error;
        ExternalTransactionId = Clean(externalTransactionId, 160) ?? ExternalTransactionId;
        FailureMessage = Clean(message, 500);
        CompletedAt ??= DateTimeOffset.UtcNow;
        Touch();
    }

    private static string CleanRequired(string value, int maxLength) =>
        Clean(value, maxLength) ?? throw new ArgumentException("La valeur obligatoire est vide.", nameof(value));

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = value.Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }
}
