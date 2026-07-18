using HomeService.Application.Abstractions;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminMissionOperationsService(IAppDbContext db)
{
    public async Task<AdminMissionOperationResult> MarkDisputedAsync(Guid missionId, string? note, CancellationToken cancellationToken)
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

        return AdminMissionOperationResult.Ok(mission, previousStatus, note.Trim());
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
