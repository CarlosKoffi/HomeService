using HomeService.Application.Abstractions;
using HomeService.Application.Auditing;
using HomeService.Application.CompanyPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminMissionDisputeService(
    IAppDbContext db,
    CompanyPortalNotificationWriter companyNotifications)
{
    public async Task<AdminMissionDisputeResult> OpenAsync(
        Guid missionId,
        string? reason,
        string? description,
        AuditActor actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return AdminMissionDisputeResult.ValidationFailed("Ajoutez une note claire pour ouvrir le litige.");
        }

        var mission = await db.Missions.FirstOrDefaultAsync(item => item.Id == missionId, cancellationToken);
        if (mission is null)
        {
            return AdminMissionDisputeResult.NotFound();
        }

        var existingOpenDispute = await db.MissionDisputes
            .FirstOrDefaultAsync(item => item.MissionId == missionId && item.Status == MissionDisputeStatus.Open, cancellationToken);
        if (existingOpenDispute is not null)
        {
            return AdminMissionDisputeResult.Ok(existingOpenDispute, mission, mission.Status, "Litige deja ouvert.");
        }

        var previousStatus = mission.Status;
        try
        {
            mission.MarkDisputed();
        }
        catch (InvalidOperationException exception)
        {
            return AdminMissionDisputeResult.ValidationFailed(exception.Message);
        }

        var parsedReason = ParseReason(reason);
        var dispute = new MissionDispute(mission.Id, MissionCancellationActor.Admin, parsedReason, description);
        db.MissionDisputes.Add(dispute);
        db.AuditLogEntries.Add(AuditLogFactory.Create(
            actor,
            "AdminMissionDisputeOpened",
            nameof(MissionDispute),
            dispute.Id,
            $"Litige ouvert sur la mission {mission.MissionNumber}.",
            auditContext,
            before: new { Status = previousStatus.ToString() },
            after: new { Status = mission.Status.ToString(), Reason = parsedReason.ToString(), Description = description.Trim() }));
        companyNotifications.AddForMission(
            mission,
            "MissionDisputeOpened",
            $"Litige ouvert sur la mission {mission.MissionNumber}",
            "Notre equipe analyse un litige sur cette mission. Vous serez informe de la decision dans votre portail.",
            "warning",
            $"missions/{mission.Id}");

        await db.SaveChangesAsync(cancellationToken);

        return AdminMissionDisputeResult.Ok(dispute, mission, previousStatus, "Litige ouvert.");
    }

    public async Task<AdminMissionDisputeResult> ResolveAsync(
        Guid missionId,
        string? resolution,
        string? note,
        int? refundPercent,
        int? refundAmount,
        AuditActor actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return AdminMissionDisputeResult.ValidationFailed("Ajoutez une note courte pour expliquer la resolution du litige.");
        }

        var mission = await db.Missions.FirstOrDefaultAsync(item => item.Id == missionId, cancellationToken);
        if (mission is null)
        {
            return AdminMissionDisputeResult.NotFound();
        }

        var dispute = await db.MissionDisputes
            .OrderByDescending(item => item.OpenedAt)
            .FirstOrDefaultAsync(item => item.MissionId == missionId && item.Status == MissionDisputeStatus.Open, cancellationToken);
        if (dispute is null)
        {
            return AdminMissionDisputeResult.ValidationFailed("Aucun litige ouvert sur cette mission.");
        }

        var previousStatus = mission.Status;
        var parsedResolution = ParseResolution(resolution);
        var refundDecision = ResolveRefundDecision(mission, parsedResolution, refundPercent, refundAmount);
        if (!refundDecision.IsValid)
        {
            return AdminMissionDisputeResult.ValidationFailed(refundDecision.Message!);
        }

        try
        {
            dispute.Resolve(
                parsedResolution,
                note,
                refundDecision.RefundPercentBasisPoints,
                refundDecision.RefundAmount,
                mission.Currency);
            mission.ResolveDispute();
        }
        catch (InvalidOperationException exception)
        {
            return AdminMissionDisputeResult.ValidationFailed(exception.Message);
        }

        db.AuditLogEntries.Add(AuditLogFactory.Create(
            actor,
            "AdminMissionDisputeResolved",
            nameof(MissionDispute),
            dispute.Id,
            $"Litige resolu sur la mission {mission.MissionNumber}.",
            auditContext,
            before: new { Status = previousStatus.ToString() },
            after: new
            {
                Status = mission.Status.ToString(),
                Resolution = dispute.Resolution?.ToString(),
                RefundPercent = dispute.RefundPercentBasisPoints.HasValue ? dispute.RefundPercentBasisPoints.Value / 100 : (int?)null,
                dispute.RefundAmount,
                dispute.Currency,
                dispute.ResolutionNote
            }));

        if (refundDecision.RefundAmount is > 0)
        {
            db.MissionFinancialBreakdowns.Add(new MissionFinancialBreakdown(
                mission.Id,
                MissionFinancialLineType.Refund,
                "Remboursement valide apres litige",
                -refundDecision.RefundAmount.Value,
                mission.Currency,
                110));
        }
        companyNotifications.AddForMission(
            mission,
            "MissionDisputeResolved",
            $"Litige resolu sur la mission {mission.MissionNumber}",
            BuildCompanyDisputeResolutionMessage(parsedResolution, refundDecision, mission.Currency),
            refundDecision.RefundAmount is > 0 ? "warning" : "success",
            $"missions/{mission.Id}");

        await db.SaveChangesAsync(cancellationToken);

        return AdminMissionDisputeResult.Ok(dispute, mission, previousStatus, "Litige resolu.");
    }

    private static MissionCancellationReason ParseReason(string? reason)
    {
        return Enum.TryParse<MissionCancellationReason>(reason, true, out var parsed)
            ? parsed
            : MissionCancellationReason.Other;
    }

    private static MissionDisputeResolution ParseResolution(string? resolution)
    {
        return Enum.TryParse<MissionDisputeResolution>(resolution, true, out var parsed)
            ? parsed
            : MissionDisputeResolution.Other;
    }

    private static string BuildCompanyDisputeResolutionMessage(
        MissionDisputeResolution resolution,
        MissionDisputeRefundDecision refundDecision,
        string currency)
    {
        return resolution switch
        {
            MissionDisputeResolution.RefundCustomer when refundDecision.RefundAmount is > 0
                => $"Remboursement client valide: {refundDecision.RefundAmount.Value:N0} {currency}.",
            MissionDisputeResolution.PartialRefund when refundDecision.RefundAmount is > 0
                => $"Remboursement partiel valide: {refundDecision.RefundAmount.Value:N0} {currency}.",
            MissionDisputeResolution.PayCompany
                => "Decision validee: le paiement entreprise est maintenu.",
            MissionDisputeResolution.NoAction
                => "Litige cloture sans action financiere.",
            _ => "Litige cloture par l'administration."
        };
    }

    private static MissionDisputeRefundDecision ResolveRefundDecision(
        Mission mission,
        MissionDisputeResolution resolution,
        int? refundPercent,
        int? refundAmount)
    {
        var totalAmount = mission.FinalTotalAmount ?? mission.CompanyQuotedAmount ?? mission.EstimatedTotalAmount ?? 0;
        if (refundPercent is < 0 or > 100)
        {
            return MissionDisputeRefundDecision.Invalid("Le pourcentage de remboursement doit etre compris entre 0 et 100.");
        }

        if (refundAmount is < 0)
        {
            return MissionDisputeRefundDecision.Invalid("Le montant de remboursement ne peut pas etre negatif.");
        }

        if (resolution is not (MissionDisputeResolution.RefundCustomer or MissionDisputeResolution.PartialRefund))
        {
            return MissionDisputeRefundDecision.Valid(null, null);
        }

        var normalizedPercent = resolution == MissionDisputeResolution.RefundCustomer
            ? refundPercent ?? 100
            : refundPercent;
        if (normalizedPercent is null && refundAmount is null)
        {
            return MissionDisputeRefundDecision.Invalid("Indiquez un pourcentage ou un montant de remboursement.");
        }

        var calculatedAmount = refundAmount ?? (int)Math.Round(totalAmount * normalizedPercent!.Value / 100m, MidpointRounding.AwayFromZero);
        if (totalAmount > 0 && calculatedAmount > totalAmount)
        {
            return MissionDisputeRefundDecision.Invalid("Le remboursement ne peut pas depasser le montant total de la mission.");
        }

        return MissionDisputeRefundDecision.Valid(
            normalizedPercent.HasValue ? normalizedPercent.Value * 100 : null,
            calculatedAmount);
    }
}

internal sealed record MissionDisputeRefundDecision(
    bool IsValid,
    string? Message,
    int? RefundPercentBasisPoints,
    int? RefundAmount)
{
    public static MissionDisputeRefundDecision Valid(int? refundPercentBasisPoints, int? refundAmount)
        => new(true, null, refundPercentBasisPoints, refundAmount);

    public static MissionDisputeRefundDecision Invalid(string message)
        => new(false, message, null, null);
}

public sealed record AdminMissionDisputeResult(
    AdminMissionOperationStatus Status,
    MissionDispute? Dispute,
    Mission? Mission,
    MissionStatus? PreviousStatus,
    string? Message)
{
    public static AdminMissionDisputeResult Ok(MissionDispute dispute, Mission mission, MissionStatus previousStatus, string message)
        => new(AdminMissionOperationStatus.Ok, dispute, mission, previousStatus, message);

    public static AdminMissionDisputeResult NotFound()
        => new(AdminMissionOperationStatus.NotFound, null, null, null, "Mission introuvable.");

    public static AdminMissionDisputeResult ValidationFailed(string message)
        => new(AdminMissionOperationStatus.ValidationFailed, null, null, null, message);
}
