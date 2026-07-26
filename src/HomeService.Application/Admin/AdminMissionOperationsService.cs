using HomeService.Application.Abstractions;
using HomeService.Application.Auditing;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminMissionOperationsService(IAppDbContext db)
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
