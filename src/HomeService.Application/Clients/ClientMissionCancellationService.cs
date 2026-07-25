using HomeService.Application.Abstractions;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Contracts.Clients;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Clients;

public sealed class ClientMissionCancellationService(IAppDbContext db)
{
    private const int DefaultAfterContactReleaseFeeAmount = 2500;

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

        try
        {
            var reason = ParseReason(request.Reason);
            var cancellationFee = mission.ContactDetailsReleasedAt is null ? 0 : DefaultAfterContactReleaseFeeAmount;
            var refundAmount = CalculateRefundAmount(mission, cancellationFee);
            mission.Cancel(
                MissionCancellationActor.Customer,
                reason,
                request.Comment,
                cancellationFee,
                refundAmount);

            TrackCancellationFinancials(mission, cancellationFee, refundAmount);
            TrackCancellationMilestone(mission, cancellationFee);
            TrackCompanyActivity(mission, reason);

            await db.SaveChangesAsync(cancellationToken);

            return ClientMissionCancellationResult.Success(new CancelClientMissionResponse(
                mission.Id,
                mission.MissionNumber,
                mission.Status.ToString(),
                mission.PaymentStatus.ToString(),
                mission.CancellationFeeAmount,
                mission.RefundAmount,
                mission.Currency,
                mission.CancelledAt!.Value));
        }
        catch (InvalidOperationException exception)
        {
            return ClientMissionCancellationResult.Invalid(exception.Message);
        }
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

    private static MissionCancellationReason ParseReason(string reason)
    {
        return Enum.TryParse<MissionCancellationReason>(reason, ignoreCase: true, out var parsed)
            ? parsed
            : MissionCancellationReason.Other;
    }

    private static int CalculateRefundAmount(Mission mission, int cancellationFee)
    {
        if (mission.PaymentStatus is not (PaymentStatus.Authorized or PaymentStatus.Paid))
        {
            return 0;
        }

        var paidAmount = mission.CompanyQuotedAmount ?? mission.EstimatedTotalAmount ?? mission.FinalTotalAmount ?? 0;
        return Math.Max(0, paidAmount - cancellationFee);
    }

    private void TrackCancellationFinancials(Mission mission, int cancellationFee, int refundAmount)
    {
        if (cancellationFee > 0 && !db.MissionFinancialBreakdowns.Local.Any(line =>
                line.MissionId == mission.Id && line.LineType == MissionFinancialLineType.CancellationFee))
        {
            db.MissionFinancialBreakdowns.Add(new MissionFinancialBreakdown(
                mission.Id,
                MissionFinancialLineType.CancellationFee,
                "Frais d'annulation",
                cancellationFee,
                mission.Currency,
                90));
        }

        if (refundAmount > 0 && !db.MissionFinancialBreakdowns.Local.Any(line =>
                line.MissionId == mission.Id && line.LineType == MissionFinancialLineType.Refund))
        {
            db.MissionFinancialBreakdowns.Add(new MissionFinancialBreakdown(
                mission.Id,
                MissionFinancialLineType.Refund,
                "Remboursement client",
                -refundAmount,
                mission.Currency,
                100));
        }
    }

    private void TrackCancellationMilestone(Mission mission, int cancellationFee)
    {
        var milestone = new MissionPaymentMilestone(
            mission.Id,
            MissionPaymentMilestoneTrigger.Cancellation,
            cancellationFee,
            mission.Currency,
            cancellationFee > 0 ? "Annulation client - frais conserves" : "Annulation client - aucun frais",
            90);

        if (cancellationFee > 0)
        {
            milestone.MarkDue(DateTimeOffset.UtcNow);
        }
        else
        {
            milestone.Cancel();
        }

        db.MissionPaymentMilestones.Add(milestone);
    }

    private void TrackCompanyActivity(Mission mission, MissionCancellationReason reason)
    {
        if (mission.CompanyId is null)
        {
            return;
        }

        db.CompanyPortalActivities.Add(new CompanyPortalActivity(
            mission.CompanyId.Value,
            "mission",
            "Mission annulee par le client",
            $"{mission.MissionNumber} - {reason}",
            "orange",
            nameof(Mission),
            mission.Id));
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
