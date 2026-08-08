using System.Text.Json;
using HomeService.Application.Abstractions;
using HomeService.Application.CompanyPortal;
using HomeService.Application.Notifications;
using HomeService.Contracts.Missions;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Missions;

public sealed class MissionCancellationWorkflowService(
    IAppDbContext db,
    CompanyPortalNotificationWriter companyNotifications,
    MobilePushNotificationQueueService mobilePushNotifications)
{
    private const int DefaultAfterContactReleaseFeeAmount = 2500;

    public async Task<MissionCancellationWorkflowResult> CancelAsync(
        Guid missionId,
        MissionCancellationActor actor,
        CancelMissionRequest request,
        Guid? expectedCompanyId,
        Guid? expectedProviderId,
        CancellationToken cancellationToken)
    {
        var validationErrors = Validate(request);
        if (validationErrors.Count > 0)
        {
            return MissionCancellationWorkflowResult.ValidationFailed(validationErrors);
        }

        var mission = await db.Missions.FirstOrDefaultAsync(item => item.Id == missionId, cancellationToken);
        if (mission is null)
        {
            return MissionCancellationWorkflowResult.NotFound("Mission introuvable.");
        }

        if (expectedCompanyId is not null && mission.CompanyId != expectedCompanyId.Value)
        {
            return MissionCancellationWorkflowResult.Forbidden("Cette mission n'appartient pas a cette entreprise.");
        }

        if (expectedProviderId is not null && mission.ProviderId != expectedProviderId.Value)
        {
            return MissionCancellationWorkflowResult.Forbidden("Cette mission n'est pas affectee a ce prestataire.");
        }

        var reason = ParseReason(request.Reason, actor);
        var fee = ResolveCancellationFee(mission, request.CancellationFeeAmount);
        var refund = CalculateRefundAmount(mission, fee);
        var previousStatus = mission.Status;

        try
        {
            mission.Cancel(actor, reason, request.Comment, fee, refund);
        }
        catch (InvalidOperationException exception)
        {
            return MissionCancellationWorkflowResult.Invalid(exception.Message);
        }

        var busyProviderIds = await CancelOpenAssignmentsAsync(mission.Id, cancellationToken);
        await RestoreProviderAvailabilityAsync(mission.Id, busyProviderIds, cancellationToken);
        TrackCancellationFinancials(mission, fee, refund);
        TrackCancellationMilestone(mission, fee);
        TrackCompanyActivity(mission, actor, reason);
        await TrackCancellationNotificationsAsync(mission, actor, reason, refund, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return MissionCancellationWorkflowResult.Success(
            ToResponse(mission),
            previousStatus);
    }

    private async Task<IReadOnlyList<Guid>> CancelOpenAssignmentsAsync(Guid missionId, CancellationToken cancellationToken)
    {
        var assignments = await db.ProviderMissionAssignments
            .Where(assignment => assignment.MissionId == missionId)
            .ToListAsync(cancellationToken);

        var busyProviderIds = assignments
            .Where(assignment => assignment.Status is ProviderMissionAssignmentStatus.Accepted or ProviderMissionAssignmentStatus.Started)
            .Select(assignment => assignment.ProviderId)
            .Distinct()
            .ToList();

        foreach (var assignment in assignments)
        {
            assignment.Cancel();
        }

        return busyProviderIds;
    }

    private async Task RestoreProviderAvailabilityAsync(
        Guid cancelledMissionId,
        IReadOnlyList<Guid> providerIds,
        CancellationToken cancellationToken)
    {
        if (providerIds.Count == 0)
        {
            return;
        }

        var providers = await db.Providers
            .Where(provider => providerIds.Contains(provider.Id))
            .ToListAsync(cancellationToken);

        foreach (var provider in providers.Where(provider => provider.Status == ProviderStatus.Approved))
        {
            var hasAnotherActiveMission = await db.ProviderMissionAssignments
                .AsNoTracking()
                .AnyAsync(assignment =>
                    assignment.ProviderId == provider.Id
                    && assignment.MissionId != cancelledMissionId
                    && assignment.Mission != null
                    && (assignment.Status == ProviderMissionAssignmentStatus.Accepted
                        || assignment.Status == ProviderMissionAssignmentStatus.Started)
                    && (assignment.Mission.Status == MissionStatus.Accepted
                        || assignment.Mission.Status == MissionStatus.OnTheWay
                        || assignment.Mission.Status == MissionStatus.Started),
                    cancellationToken);

            if (!hasAnotherActiveMission)
            {
                provider.SetAvailability(true, provider.CurrentLatitude, provider.CurrentLongitude);
            }
        }
    }

    private static IReadOnlyList<string> Validate(CancelMissionRequest request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            errors.Add("La raison d'annulation est obligatoire.");
        }

        if (request.Comment?.Length > 1200)
        {
            errors.Add("Le commentaire d'annulation est trop long.");
        }

        if (request.CancellationFeeAmount is < 0)
        {
            errors.Add("Les frais d'annulation ne peuvent pas etre negatifs.");
        }

        return errors;
    }

    private static MissionCancellationReason ParseReason(string reason, MissionCancellationActor actor)
    {
        if (Enum.TryParse<MissionCancellationReason>(reason, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return actor switch
        {
            MissionCancellationActor.Provider => MissionCancellationReason.ProviderUnavailable,
            MissionCancellationActor.Company => MissionCancellationReason.CompanyUnavailable,
            _ => MissionCancellationReason.Other
        };
    }

    private static int ResolveCancellationFee(Mission mission, int? requestedFee)
    {
        if (mission.ContactDetailsReleasedAt is null)
        {
            return 0;
        }

        return Math.Max(0, requestedFee ?? DefaultAfterContactReleaseFeeAmount);
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
        if (cancellationFee > 0)
        {
            db.MissionFinancialBreakdowns.Add(new MissionFinancialBreakdown(
                mission.Id,
                MissionFinancialLineType.CancellationFee,
                "Frais d'annulation",
                cancellationFee,
                mission.Currency,
                90));
        }

        if (refundAmount > 0)
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
            cancellationFee > 0 ? "Annulation - frais conserves" : "Annulation - aucun frais",
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

    private void TrackCompanyActivity(Mission mission, MissionCancellationActor actor, MissionCancellationReason reason)
    {
        if (mission.CompanyId is null)
        {
            return;
        }

        db.CompanyPortalActivities.Add(new CompanyPortalActivity(
            mission.CompanyId.Value,
            "mission",
            actor == MissionCancellationActor.Customer
                ? "Mission annulée par le client"
                : "Mission annulée",
            $"{mission.MissionNumber} - {DescribeActor(actor)}. Motif : {DescribeReason(reason)}."
                + (string.IsNullOrWhiteSpace(mission.CancellationComment)
                    ? string.Empty
                    : $" Détail : {mission.CancellationComment}."),
            "orange",
            nameof(Mission),
            mission.Id));
    }

    private async Task TrackCancellationNotificationsAsync(
        Mission mission,
        MissionCancellationActor actor,
        MissionCancellationReason reason,
        int refundAmount,
        CancellationToken cancellationToken)
    {
        var assignmentId = mission.ProviderId is null
            ? null
            : await db.ProviderMissionAssignments
                .AsNoTracking()
                .Where(item => item.MissionId == mission.Id && item.ProviderId == mission.ProviderId)
                .OrderByDescending(item => item.CreatedAt)
                .Select(item => (Guid?)item.Id)
                .FirstOrDefaultAsync(cancellationToken);
        var metadataJson = JsonSerializer.Serialize(new
        {
            type = "mission_cancelled",
            missionId = mission.Id,
            missionNumber = mission.MissionNumber,
            assignmentId,
            providerId = mission.ProviderId,
            companyId = mission.CompanyId,
            cancelledBy = actor.ToString(),
            reason = reason.ToString(),
            comment = mission.CancellationComment
        });
        var actorLabel = DescribeActor(actor);
        var reasonLabel = DescribeReason(reason);
        var commentText = string.IsNullOrWhiteSpace(mission.CancellationComment)
            ? string.Empty
            : $" Détail : {mission.CancellationComment}.";
        var title = actor == MissionCancellationActor.Customer
            ? "Mission annulée par le client"
            : $"Mission annulée par {actorLabel.ToLowerInvariant()}";
        var body = $"{actorLabel} a annulé la mission {mission.MissionNumber}. Motif : {reasonLabel}.{commentText}";

        companyNotifications.AddForMission(
            mission,
            "MissionCancelled",
            title,
            BuildCompanyCancellationMessage(mission, actor, reason, refundAmount),
            refundAmount > 0 ? "warning" : "danger",
            $"missions/{mission.Id}");

        await mobilePushNotifications.QueueForOwnerAsync(
            MobileDeviceOwnerType.Customer,
            mission.CustomerId,
            "Mission annulée",
            actor == MissionCancellationActor.Customer
                ? $"Votre mission {mission.MissionNumber} a bien été annulée. Motif : {reasonLabel}."
                : body,
            nameof(Mission),
            mission.Id,
            metadataJson,
            cancellationToken,
            saveChanges: false);

        if (mission.CompanyId.HasValue)
        {
            await mobilePushNotifications.QueueForOwnerAsync(
                MobileDeviceOwnerType.Company,
                mission.CompanyId.Value,
                title,
                body,
                nameof(Mission),
                mission.Id,
                metadataJson,
                cancellationToken,
                saveChanges: false);
        }

        if (mission.ProviderId is null)
        {
            return;
        }

        await mobilePushNotifications.QueueForOwnerAsync(
            MobileDeviceOwnerType.Provider,
            mission.ProviderId.Value,
            title,
            body,
            nameof(Mission),
            mission.Id,
            metadataJson,
            cancellationToken,
            saveChanges: false);
    }

    private static string BuildCompanyCancellationMessage(
        Mission mission,
        MissionCancellationActor actor,
        MissionCancellationReason reason,
        int refundAmount)
    {
        var refundText = refundAmount > 0
            ? $" Remboursement client prévu : {refundAmount:N0} {mission.Currency}."
            : " Aucun remboursement automatique n'est prevu.";
        var commentText = string.IsNullOrWhiteSpace(mission.CancellationComment)
            ? string.Empty
            : $" Détail communiqué : {mission.CancellationComment}.";

        return $"Annulation par {DescribeActor(actor)}. Motif : {DescribeReason(reason)}.{commentText}{refundText}";
    }

    private static string DescribeActor(MissionCancellationActor actor) => actor switch
    {
        MissionCancellationActor.Customer => "Le client",
        MissionCancellationActor.Provider => "Le prestataire",
        MissionCancellationActor.Company => "L'entreprise",
        MissionCancellationActor.Admin => "L'administration",
        _ => "Un intervenant"
    };

    private static string DescribeReason(MissionCancellationReason reason) => reason switch
    {
        MissionCancellationReason.CustomerChangedMind => "le client n'a plus besoin de l'intervention",
        MissionCancellationReason.CustomerUnavailable => "le client est indisponible",
        MissionCancellationReason.CustomerAbsent => "le client est absent",
        MissionCancellationReason.AccessRefused => "l'accès au lieu d'intervention est impossible",
        MissionCancellationReason.ProviderUnavailable => "le prestataire est indisponible",
        MissionCancellationReason.ProviderNoShow => "le prestataire ne s'est pas présenté",
        MissionCancellationReason.CompanyUnavailable => "l'entreprise est indisponible",
        MissionCancellationReason.Other => "autre raison",
        _ => reason.ToString()
    };

    private static CancelMissionResponse ToResponse(Mission mission)
    {
        return new CancelMissionResponse(
            mission.Id,
            mission.MissionNumber,
            mission.Status.ToString(),
            mission.PaymentStatus.ToString(),
            mission.CancelledBy?.ToString() ?? string.Empty,
            mission.CancellationReason?.ToString() ?? string.Empty,
            mission.CancellationFeeAmount,
            mission.RefundAmount,
            mission.Currency,
            mission.CancelledAt!.Value);
    }
}

public enum MissionCancellationWorkflowStatus
{
    Success,
    NotFound,
    Forbidden,
    ValidationFailed,
    Invalid
}

public sealed record MissionCancellationWorkflowResult(
    MissionCancellationWorkflowStatus Status,
    CancelMissionResponse? Response,
    MissionStatus? PreviousStatus,
    string Message,
    IReadOnlyList<string> Errors)
{
    public bool IsSuccess => Status == MissionCancellationWorkflowStatus.Success;

    public static MissionCancellationWorkflowResult Success(CancelMissionResponse response, MissionStatus previousStatus)
        => new(MissionCancellationWorkflowStatus.Success, response, previousStatus, "Mission annulee.", []);

    public static MissionCancellationWorkflowResult NotFound(string message)
        => new(MissionCancellationWorkflowStatus.NotFound, null, null, message, []);

    public static MissionCancellationWorkflowResult Forbidden(string message)
        => new(MissionCancellationWorkflowStatus.Forbidden, null, null, message, []);

    public static MissionCancellationWorkflowResult ValidationFailed(IReadOnlyList<string> errors)
        => new(MissionCancellationWorkflowStatus.ValidationFailed, null, null, "Annulation invalide.", errors);

    public static MissionCancellationWorkflowResult Invalid(string message)
        => new(MissionCancellationWorkflowStatus.Invalid, null, null, message, []);
}
