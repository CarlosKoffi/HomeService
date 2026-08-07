using HomeService.Application.Abstractions;
using HomeService.Application.CompanyPortal;
using HomeService.Application.Missions;
using HomeService.Application.Notifications;
using HomeService.Contracts.Clients;
using HomeService.Contracts.Missions;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Clients;

public sealed class ClientMissionCancellationService(
    IAppDbContext db,
    MissionCancellationWorkflowService? cancellationWorkflow = null)
{
    public async Task<ClientMissionCancellationResult> CancelAsync(
        Guid missionId,
        CancelClientMissionRequest request,
        CancellationToken cancellationToken)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return ClientMissionCancellationResult.ValidationFailed(errors);
        }

        var mission = await db.Missions
            .FirstOrDefaultAsync(item => item.Id == missionId, cancellationToken);

        if (mission is null)
        {
            return ClientMissionCancellationResult.NotFound("Mission introuvable.");
        }

        var customer = await db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == mission.CustomerId, cancellationToken);
        if (customer is null)
        {
            return ClientMissionCancellationResult.Invalid("Client introuvable pour cette mission.");
        }

        if (!PhoneMatches(customer.PhoneNumber, request.CustomerPhoneNumber))
        {
            return ClientMissionCancellationResult.Forbidden("Ce numero ne correspond pas au client de la mission.");
        }

        var workflow = cancellationWorkflow ?? new MissionCancellationWorkflowService(
            db,
            new CompanyPortalNotificationWriter(db),
            new MobilePushNotificationQueueService(db));
        var cancellationResult = await workflow.CancelAsync(
            mission.Id,
            Domain.Enums.MissionCancellationActor.Customer,
            new CancelMissionRequest(request.Reason, request.Comment),
            expectedCompanyId: null,
            expectedProviderId: null,
            cancellationToken);

        if (!cancellationResult.IsSuccess || cancellationResult.Response is null)
        {
            return cancellationResult.Status switch
            {
                MissionCancellationWorkflowStatus.NotFound => ClientMissionCancellationResult.NotFound(cancellationResult.Message),
                MissionCancellationWorkflowStatus.Forbidden => ClientMissionCancellationResult.Forbidden(cancellationResult.Message),
                MissionCancellationWorkflowStatus.ValidationFailed => ClientMissionCancellationResult.ValidationFailed(cancellationResult.Errors),
                _ => ClientMissionCancellationResult.Invalid(cancellationResult.Message)
            };
        }

        var response = cancellationResult.Response;
        return ClientMissionCancellationResult.Success(new CancelClientMissionResponse(
            response.MissionId,
            response.MissionNumber,
            response.Status,
            response.PaymentStatus,
            response.CancellationFeeAmount,
            response.RefundAmount,
            response.Currency,
            response.CancelledAt));
    }

    private static IReadOnlyList<string> Validate(CancelClientMissionRequest request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.CustomerPhoneNumber))
        {
            errors.Add("Le numero client est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            errors.Add("La raison d'annulation est obligatoire.");
        }

        if (request.Comment?.Length > 1200)
        {
            errors.Add("Le commentaire d'annulation est trop long.");
        }

        return errors;
    }

    private static bool PhoneMatches(string? storedPhone, string inputPhone)
    {
        return NormalizePhone(storedPhone) == NormalizePhone(inputPhone);
    }

    private static string NormalizePhone(string? phone)
    {
        return new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
    }
}

public enum ClientMissionCancellationStatus
{
    Success,
    NotFound,
    Forbidden,
    ValidationFailed,
    Invalid
}

public sealed record ClientMissionCancellationResult(
    ClientMissionCancellationStatus Status,
    CancelClientMissionResponse? Response,
    string Message,
    IReadOnlyList<string> Errors)
{
    public bool IsSuccess => Status == ClientMissionCancellationStatus.Success;

    public static ClientMissionCancellationResult Success(CancelClientMissionResponse response)
        => new(ClientMissionCancellationStatus.Success, response, "Mission annulee.", []);

    public static ClientMissionCancellationResult NotFound(string message)
        => new(ClientMissionCancellationStatus.NotFound, null, message, []);

    public static ClientMissionCancellationResult Forbidden(string message)
        => new(ClientMissionCancellationStatus.Forbidden, null, message, []);

    public static ClientMissionCancellationResult ValidationFailed(IReadOnlyList<string> errors)
        => new(ClientMissionCancellationStatus.ValidationFailed, null, "Annulation invalide.", errors);

    public static ClientMissionCancellationResult Invalid(string message)
        => new(ClientMissionCancellationStatus.Invalid, null, message, []);
}
