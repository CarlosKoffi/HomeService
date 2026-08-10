namespace HomeService.Application.Abstractions;

public interface IClientPaymentGateway
{
    bool IsEnabled { get; }
    int FeeRateBasisPoints { get; }

    Task<ClientPaymentGatewayResult> CreateAsync(
        ClientPaymentGatewayRequest request,
        CancellationToken cancellationToken);

    Task<ClientPaymentGatewayResult> GetStatusAsync(
        string externalPaymentRequestId,
        CancellationToken cancellationToken);
}

public sealed record ClientPaymentGatewayRequest(
    Guid LocalPaymentRequestId,
    Guid MissionId,
    string Reference,
    string PaymentMethod,
    int Amount,
    string Currency);

public sealed record ClientPaymentGatewayResult(
    bool Accepted,
    bool IsDefinitive,
    string Status,
    string? ExternalPaymentRequestId,
    string? ExternalTransactionId,
    string? RedirectUrl,
    string? Message,
    DateTimeOffset? ExpiresAt,
    int? Amount = null,
    string? Currency = null)
{
    public static ClientPaymentGatewayResult Disabled() =>
        new(false, true, "error", null, null, null, "Les paiements Jeko ne sont pas actives.", null);
}
