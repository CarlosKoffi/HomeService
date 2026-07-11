using HomeService.Contracts.ProviderPortal;

namespace HomeService.Application.ProviderPortal;

public sealed record ProviderMissionOperationResult(
    ProviderMissionOperationStatus Status,
    ProviderLocationVerificationResponse? Response,
    string? Message)
{
    public static ProviderMissionOperationResult Ok(ProviderLocationVerificationResponse response)
    {
        return new ProviderMissionOperationResult(ProviderMissionOperationStatus.Ok, response, null);
    }

    public static ProviderMissionOperationResult BadRequest(string message, ProviderLocationVerificationResponse? response = null)
    {
        return new ProviderMissionOperationResult(ProviderMissionOperationStatus.BadRequest, response, message);
    }

    public static ProviderMissionOperationResult Forbidden(string message)
    {
        return new ProviderMissionOperationResult(ProviderMissionOperationStatus.Forbidden, null, message);
    }

    public static ProviderMissionOperationResult NotFound(string message)
    {
        return new ProviderMissionOperationResult(ProviderMissionOperationStatus.NotFound, null, message);
    }
}
