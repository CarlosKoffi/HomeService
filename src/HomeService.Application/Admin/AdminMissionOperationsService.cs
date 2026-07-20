using HomeService.Application.Abstractions;
using HomeService.Application.Auditing;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminMissionOperationsService(IAppDbContext db)
{
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
