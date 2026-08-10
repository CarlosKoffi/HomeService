using HomeService.Domain.Enums;

namespace HomeService.Application.Abstractions;

public interface ICompanyPayoutGateway
{
    bool IsEnabled { get; }
    Task<CompanyPayoutGatewayResult> CreateAsync(CompanyPayoutGatewayRequest request, CancellationToken cancellationToken);
    Task<CompanyPayoutGatewayResult> GetStatusAsync(string externalTransactionId, CancellationToken cancellationToken);
}

public sealed record CompanyPayoutGatewayRequest(
    string Reference,
    CompanyPayoutMethod Method,
    string ProviderCode,
    string BeneficiaryName,
    string Identifier,
    int Amount,
    string Currency);

public sealed record CompanyPayoutGatewayResult(
    bool IsAccepted,
    bool IsFinal,
    bool IsSuccessful,
    string Status,
    string? ExternalTransactionId,
    string? Message)
{
    public static CompanyPayoutGatewayResult Disabled() =>
        new(false, false, false, "disabled", null, "La passerelle de reversement est desactivee.");
}
