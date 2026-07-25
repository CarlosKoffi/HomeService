using HomeService.Application.Abstractions;
using HomeService.Application.Auditing;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminMissionDisputeService(IAppDbContext db)
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

        await db.SaveChangesAsync(cancellationToken);

        return AdminMissionDisputeResult.Ok(dispute, mission, previousStatus, "Litige ouvert.");
    }

    public async Task<AdminMissionDisputeResult> ResolveAsync(
        Guid missionId,
        string? resolution,
        string? note,
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
        try
        {
            dispute.Resolve(ParseResolution(resolution), note);
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
                dispute.ResolutionNote
            }));

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
