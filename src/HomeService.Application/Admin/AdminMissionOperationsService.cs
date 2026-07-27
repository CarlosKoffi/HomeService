using HomeService.Application.Abstractions;
using HomeService.Application.Auditing;
using HomeService.Application.CompanyPortal;
using HomeService.Application.Notifications;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminMissionOperationsService(
    IAppDbContext db,
    CompanyPortalNotificationWriter companyNotifications,
    MobilePushNotificationQueueService mobilePushNotifications,
    NotificationDeliveryPreferenceService deliveryPreferences,
    NotificationTemplateService notificationTemplates)
{
    public async Task<AdminMissionOperationResult> CancelAsync(
        Guid missionId,
        string? reason,
        string? note,
        int? cancellationFeeAmount,
        AuditActor actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        var mission = await db.Missions.FirstOrDefaultAsync(item => item.Id == missionId, cancellationToken);
        if (mission is null)
        {
            return AdminMissionOperationResult.NotFound();
        }

        if (string.IsNullOrWhiteSpace(note))
        {
            return AdminMissionOperationResult.ValidationFailed("Ajoutez une note courte pour expliquer l'annulation.");
        }

        var previousStatus = mission.Status;
        var parsedReason = ParseCancellationReason(reason, MissionCancellationActor.Admin);
        var refundBase = mission.CompanyQuotedAmount ?? mission.FinalTotalAmount ?? mission.EstimatedTotalAmount ?? 0;
        var feeDecision = ResolveCancellationFee(mission, refundBase, cancellationFeeAmount);
        if (!feeDecision.IsValid)
        {
            return AdminMissionOperationResult.ValidationFailed(feeDecision.Message!);
        }

        var refund = mission.PaymentStatus is PaymentStatus.Authorized or PaymentStatus.Paid
            ? Math.Max(0, refundBase - feeDecision.FeeAmount)
            : 0;

        try
        {
            mission.Cancel(MissionCancellationActor.Admin, parsedReason, note, feeDecision.FeeAmount, refund);
        }
        catch (InvalidOperationException exception)
        {
            return AdminMissionOperationResult.ValidationFailed(exception.Message);
        }

        var assignments = await db.ProviderMissionAssignments
            .Where(assignment => assignment.MissionId == missionId)
            .ToListAsync(cancellationToken);
        foreach (var assignment in assignments)
        {
            assignment.Cancel();
        }

        var cleanNote = note.Trim();
        AddMissionAudit(
            actor,
            auditContext,
            "AdminMissionCancelled",
            mission,
            previousStatus,
            cleanNote,
            $"Mission annulee par l'administration. Note: {cleanNote}");
        companyNotifications.AddForMission(
            mission,
            "MissionCancelled",
            $"Mission {mission.MissionNumber} annulee",
            $"La mission a ete annulee par l'administration. Motif: {cleanNote}",
            "warning",
            $"missions/{mission.Id}");
        await TrackCancellationFinancialsAsync(
            mission,
            feeDecision.FeeAmount,
            refund,
            cancellationToken);
        await QueueCustomerCancellationNotificationsAsync(mission, cleanNote, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return AdminMissionOperationResult.Ok(mission, previousStatus, cleanNote);
    }

    public async Task<AdminMissionOperationResult> MarkDisputedAsync(
        Guid missionId,
        string? note,
        AuditActor actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        var mission = await db.Missions.FirstOrDefaultAsync(item => item.Id == missionId, cancellationToken);
        if (mission is null)
        {
            return AdminMissionOperationResult.NotFound();
        }

        if (string.IsNullOrWhiteSpace(note))
        {
            return AdminMissionOperationResult.ValidationFailed("Ajoutez une note courte pour expliquer le litige.");
        }

        var previousStatus = mission.Status;
        try
        {
            mission.MarkDisputed();
        }
        catch (InvalidOperationException exception)
        {
            return AdminMissionOperationResult.ValidationFailed(exception.Message);
        }

        var cleanNote = note.Trim();
        AddMissionAudit(
            actor,
            auditContext,
            "AdminMissionMarkedDisputed",
            mission,
            previousStatus,
            cleanNote,
            $"Mission marquee en litige. Note: {cleanNote}");

        await db.SaveChangesAsync(cancellationToken);

        return AdminMissionOperationResult.Ok(mission, previousStatus, cleanNote);
    }

    public async Task<AdminMissionOperationResult> ResolveDisputeAsync(
        Guid missionId,
        string? note,
        AuditActor actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        var mission = await db.Missions.FirstOrDefaultAsync(item => item.Id == missionId, cancellationToken);
        if (mission is null)
        {
            return AdminMissionOperationResult.NotFound();
        }

        if (string.IsNullOrWhiteSpace(note))
        {
            return AdminMissionOperationResult.ValidationFailed("Ajoutez une note courte pour expliquer la resolution du litige.");
        }

        var previousStatus = mission.Status;
        try
        {
            mission.ResolveDispute();
        }
        catch (InvalidOperationException exception)
        {
            return AdminMissionOperationResult.ValidationFailed(exception.Message);
        }

        var cleanNote = note.Trim();
        AddMissionAudit(
            actor,
            auditContext,
            "AdminMissionDisputeResolved",
            mission,
            previousStatus,
            cleanNote,
            $"Litige mission resolu. Note: {cleanNote}");

        await db.SaveChangesAsync(cancellationToken);

        return AdminMissionOperationResult.Ok(mission, previousStatus, cleanNote);
    }

    private void AddMissionAudit(
        AuditActor actor,
        AuditRequestContext? auditContext,
        string action,
        Mission mission,
        MissionStatus previousStatus,
        string note,
        string summary)
    {
        db.AuditLogEntries.Add(AuditLogFactory.Create(
            actor,
            action,
            nameof(Mission),
            mission.Id,
            summary,
            auditContext,
            before: new { Status = previousStatus.ToString() },
            after: new { Status = mission.Status.ToString(), Note = note }));
    }

    private async Task QueueCustomerCancellationNotificationsAsync(
        Mission mission,
        string cancellationNote,
        CancellationToken cancellationToken)
    {
        var customer = await db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == mission.CustomerId, cancellationToken);
        if (customer is null)
        {
            return;
        }

        const string eventKey = "MissionCancelled";
        var variables = NotificationTemplateRenderer.Variables(
            ("NomClient", $"{customer.FirstName} {customer.LastName}".Trim()),
            ("NumeroMission", mission.MissionNumber),
            ("Motif", cancellationNote),
            ("DescriptionService", mission.Description),
            ("Montant", $"{mission.RefundAmount:N0} {mission.Currency}"));
        var preference = await deliveryPreferences.GetAsync(
            eventKey,
            "Customer",
            defaultEmailEnabled: false,
            defaultWhatsAppEnabled: true,
            cancellationToken);
        var metadataJson = $$"""{"missionId":"{{mission.Id}}","missionNumber":"{{mission.MissionNumber}}","refundAmount":{{mission.RefundAmount}}}""";

        if (preference.MobileAppEnabled)
        {
            var push = await notificationTemplates.RenderAsync(
                eventKey,
                NotificationTemplateChannel.MobilePush,
                "Mission annulee",
                $"La mission {mission.MissionNumber} a ete annulee. {cancellationNote}",
                variables,
                cancellationToken);

            await mobilePushNotifications.QueueForOwnerAsync(
                MobileDeviceOwnerType.Customer,
                mission.CustomerId,
                push.Subject,
                push.Body,
                nameof(Mission),
                mission.Id,
                metadataJson,
                cancellationToken,
                saveChanges: false);
        }

        if (preference.WhatsAppEnabled && !string.IsNullOrWhiteSpace(customer.PhoneNumber))
        {
            var whatsApp = await notificationTemplates.RenderAsync(
                eventKey,
                NotificationTemplateChannel.WhatsApp,
                "Mission annulee",
                $"La mission {mission.MissionNumber} a ete annulee. {cancellationNote}",
                variables,
                cancellationToken);

            db.NotificationOutboxMessages.Add(new NotificationOutboxMessage(
                NotificationChannel.WhatsApp,
                customer.PhoneNumber,
                whatsApp.Subject,
                whatsApp.Body,
                nameof(Mission),
                mission.Id,
                metadataJson));
        }

    }

    private async Task TrackCancellationFinancialsAsync(
        Mission mission,
        int cancellationFee,
        int refundAmount,
        CancellationToken cancellationToken)
    {
        if (cancellationFee > 0 && !await HasFinancialLineAsync(mission.Id, MissionFinancialLineType.CancellationFee, cancellationToken))
        {
            db.MissionFinancialBreakdowns.Add(new MissionFinancialBreakdown(
                mission.Id,
                MissionFinancialLineType.CancellationFee,
                "Frais d'annulation admin",
                cancellationFee,
                mission.Currency,
                90));
        }

        if (refundAmount > 0 && !await HasFinancialLineAsync(mission.Id, MissionFinancialLineType.Refund, cancellationToken))
        {
            db.MissionFinancialBreakdowns.Add(new MissionFinancialBreakdown(
                mission.Id,
                MissionFinancialLineType.Refund,
                "Remboursement client apres annulation admin",
                -refundAmount,
                mission.Currency,
                100));
        }
    }

    private Task<bool> HasFinancialLineAsync(
        Guid missionId,
        MissionFinancialLineType lineType,
        CancellationToken cancellationToken)
    {
        return db.MissionFinancialBreakdowns.AnyAsync(
            line => line.MissionId == missionId && line.LineType == lineType,
            cancellationToken);
    }

    private static MissionCancellationReason ParseCancellationReason(string? reason, MissionCancellationActor actor)
    {
        if (Enum.TryParse<MissionCancellationReason>(reason, true, out var parsed))
        {
            return parsed;
        }

        return actor == MissionCancellationActor.Admin
            ? MissionCancellationReason.Other
            : MissionCancellationReason.Other;
    }

    private static AdminCancellationFeeDecision ResolveCancellationFee(
        Mission mission,
        int refundBase,
        int? requestedFeeAmount)
    {
        if (requestedFeeAmount is < 0)
        {
            return AdminCancellationFeeDecision.Invalid("Les frais d'annulation ne peuvent pas etre negatifs.");
        }

        if (mission.ContactDetailsReleasedAt is null)
        {
            return AdminCancellationFeeDecision.Valid(0);
        }

        var feeAmount = requestedFeeAmount ?? mission.CancellationFeeAmount;
        if (feeAmount > Math.Max(0, refundBase))
        {
            return AdminCancellationFeeDecision.Invalid("Les frais d'annulation ne peuvent pas depasser le montant de la mission.");
        }

        return AdminCancellationFeeDecision.Valid(Math.Max(0, feeAmount));
    }
}

internal sealed record AdminCancellationFeeDecision(bool IsValid, string? Message, int FeeAmount)
{
    public static AdminCancellationFeeDecision Valid(int feeAmount)
        => new(true, null, feeAmount);

    public static AdminCancellationFeeDecision Invalid(string message)
        => new(false, message, 0);
}

public sealed record AdminMissionOperationResult(
    AdminMissionOperationStatus Status,
    Mission? Mission,
    MissionStatus? PreviousStatus,
    string? Note,
    string? Message)
{
    public static AdminMissionOperationResult Ok(Mission mission, MissionStatus previousStatus, string note)
        => new(AdminMissionOperationStatus.Ok, mission, previousStatus, note, null);

    public static AdminMissionOperationResult NotFound()
        => new(AdminMissionOperationStatus.NotFound, null, null, null, "Mission introuvable.");

    public static AdminMissionOperationResult ValidationFailed(string message)
        => new(AdminMissionOperationStatus.ValidationFailed, null, null, null, message);
}

public enum AdminMissionOperationStatus
{
    Ok,
    NotFound,
    ValidationFailed
}
