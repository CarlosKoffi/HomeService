using HomeService.Application.Abstractions;
using HomeService.Contracts.ProviderPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Quality;

public sealed class MissionQualityChecklistService(IAppDbContext db)
{
    public async Task<ProviderMissionQualityChecklistResponse?> GetForProviderAsync(
        Guid providerId,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var assignment = await db.ProviderMissionAssignments
            .Include(item => item.Mission)
            .FirstOrDefaultAsync(item => item.Id == assignmentId && item.ProviderId == providerId, cancellationToken);
        if (assignment?.Mission is null) return null;

        var control = await EnsureControlAsync(assignment.Mission, cancellationToken);
        if (control is null)
        {
            return EmptyChecklist();
        }

        await ApplyAutomaticResponsesAsync(control, assignment, cancellationToken);
        return Map(control);
    }

    public async Task<QualityChecklistOperationResult> RespondAsync(
        Guid providerId,
        Guid assignmentId,
        Guid itemId,
        UpdateProviderMissionQualityItemRequest request,
        CancellationToken cancellationToken)
    {
        var assignment = await db.ProviderMissionAssignments
            .Include(item => item.Mission)
            .FirstOrDefaultAsync(item => item.Id == assignmentId && item.ProviderId == providerId, cancellationToken);
        if (assignment?.Mission is null)
        {
            return QualityChecklistOperationResult.NotFound("Mission introuvable pour ce prestataire.");
        }

        if (assignment.Mission.Status is MissionStatus.Completed or MissionStatus.Cancelled or MissionStatus.Disputed or MissionStatus.Resolved)
        {
            return QualityChecklistOperationResult.Invalid("La checklist est verrouillee car la mission est fermee.");
        }

        var control = await EnsureControlAsync(assignment.Mission, cancellationToken);
        if (control is null)
        {
            return QualityChecklistOperationResult.Invalid("Aucune checklist qualite ne s'applique a cette prestation.");
        }

        var item = await db.MissionQualityItems
            .FirstOrDefaultAsync(candidate => candidate.Id == itemId && candidate.ControlId == control.Id, cancellationToken);
        if (item is null)
        {
            return QualityChecklistOperationResult.NotFound("Controle qualite introuvable.");
        }

        if (item.ResponseType == QualityChecklistResponseType.Automatic)
        {
            return QualityChecklistOperationResult.Invalid("Ce controle est renseigne automatiquement par la plateforme.");
        }

        if (item.Stage != QualityChecklistStage.BeforeStart
            && assignment.Mission.Status != MissionStatus.Started)
        {
            return QualityChecklistOperationResult.Invalid("Ce controle sera disponible apres le demarrage de la mission.");
        }

        if (request.EvidenceAttachmentId.HasValue)
        {
            var evidenceExists = await db.MissionAttachments
                .AsNoTracking()
                .AnyAsync(attachment => attachment.Id == request.EvidenceAttachmentId.Value
                    && attachment.MissionId == assignment.MissionId
                    && !attachment.IsDeleted,
                    cancellationToken);
            if (!evidenceExists)
            {
                return QualityChecklistOperationResult.Invalid("La photo fournie n'appartient pas a cette mission.");
            }
        }

        item.Respond(request.BooleanValue, request.NumberValue, request.TextValue, request.EvidenceAttachmentId);
        control.MarkInProgress();
        await db.SaveChangesAsync(cancellationToken);
        return QualityChecklistOperationResult.Ok(Map(control));
    }

    public async Task<QualityGateResult> ValidateCanStartAsync(
        Guid providerId,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        return await ValidateGateAsync(providerId, assignmentId, beforeCompletion: false, cancellationToken);
    }

    public async Task<QualityGateResult> ValidateCanCompleteAsync(
        Guid providerId,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        return await ValidateGateAsync(providerId, assignmentId, beforeCompletion: true, cancellationToken);
    }

    public async Task LockAfterCompletionAsync(Guid missionId, CancellationToken cancellationToken)
    {
        var control = await db.MissionQualityControls
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.MissionId == missionId, cancellationToken);
        if (control is null) return;

        if (control.Items.All(item => !item.IsRequired || item.IsCompleted))
        {
            control.MarkCompleted();
        }

        control.Lock();
    }

    public async Task<MissionQualityControl?> EnsureControlAsync(Mission mission, CancellationToken cancellationToken)
    {
        var existing = await db.MissionQualityControls
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.MissionId == mission.Id, cancellationToken);
        if (existing is not null) return existing;

        var template = await db.QualityChecklistTemplates
            .Include(item => item.Items)
            .Where(item => item.IsActive
                && item.ServiceId == mission.ServiceId
                && (item.ServicePrestationId == mission.ServicePrestationId || item.ServicePrestationId == null))
            .OrderByDescending(item => item.ServicePrestationId == mission.ServicePrestationId)
            .ThenByDescending(item => item.Version)
            .FirstOrDefaultAsync(cancellationToken);
        if (template is null) return null;

        var sourceItems = template.Items
            .Where(item => item.IsActive
                && (item.ServiceOptionId == null || item.ServiceOptionId == mission.ServiceOptionId))
            .OrderBy(item => item.Stage)
            .ThenBy(item => item.SortOrder)
            .ToList();
        if (sourceItems.Count == 0) return null;

        var control = new MissionQualityControl(mission.Id, template.Id, template.Version);
        db.MissionQualityControls.Add(control);
        foreach (var sourceItem in sourceItems)
        {
            db.MissionQualityItems.Add(new MissionQualityItem(control.Id, sourceItem));
        }

        await db.SaveChangesAsync(cancellationToken);
        return await db.MissionQualityControls
            .Include(item => item.Items)
            .FirstAsync(item => item.Id == control.Id, cancellationToken);
    }

    private async Task<QualityGateResult> ValidateGateAsync(
        Guid providerId,
        Guid assignmentId,
        bool beforeCompletion,
        CancellationToken cancellationToken)
    {
        var assignment = await db.ProviderMissionAssignments
            .Include(item => item.Mission)
            .FirstOrDefaultAsync(item => item.Id == assignmentId && item.ProviderId == providerId, cancellationToken);
        if (assignment?.Mission is null)
        {
            return QualityGateResult.Blocked("Mission introuvable pour ce prestataire.", []);
        }

        var control = await EnsureControlAsync(assignment.Mission, cancellationToken);
        if (control is null) return QualityGateResult.Allowed();

        await ApplyAutomaticResponsesAsync(control, assignment, cancellationToken);
        var missing = control.Items
            .Where(item => item.IsRequired
                && !item.IsCompleted
                && (beforeCompletion || item.Stage == QualityChecklistStage.BeforeStart))
            .OrderBy(item => item.Stage)
            .ThenBy(item => item.SortOrder)
            .Select(item => item.Label)
            .ToList();

        return missing.Count == 0
            ? QualityGateResult.Allowed()
            : QualityGateResult.Blocked(
                beforeCompletion
                    ? "Terminez les controles qualite obligatoires avant de cloturer la mission."
                    : "Terminez les controles de debut avant de demarrer la mission.",
                missing);
    }

    private async Task ApplyAutomaticResponsesAsync(
        MissionQualityControl control,
        ProviderMissionAssignment assignment,
        CancellationToken cancellationToken)
    {
        var changed = false;
        foreach (var item in control.Items.Where(item => item.ResponseType == QualityChecklistResponseType.Automatic && !item.IsCompleted))
        {
            var value = item.Code switch
            {
                "payment-confirmed" => assignment.Mission?.IsInitialPaymentConfirmed == true,
                "arrival-verified" => assignment.HasVerifiedArrival,
                "mission-started" => assignment.Status == ProviderMissionAssignmentStatus.Started,
                _ => false
            };
            if (!value) continue;
            item.Respond(true, null, null, null);
            changed = true;
        }

        if (changed)
        {
            control.MarkInProgress();
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static ProviderMissionQualityChecklistResponse Map(MissionQualityControl control)
    {
        var items = control.Items.OrderBy(item => item.Stage).ThenBy(item => item.SortOrder).ToList();
        var stages = Enum.GetValues<QualityChecklistStage>()
            .Select(stage =>
            {
                var stageItems = items.Where(item => item.Stage == stage).ToList();
                return new ProviderMissionQualityStageResponse(
                    stage.ToString(),
                    StageLabel(stage),
                    stageItems.Count(item => item.IsRequired),
                    stageItems.Count(item => item.IsRequired && item.IsCompleted),
                    stageItems.Select(item => new ProviderMissionQualityItemResponse(
                        item.Id,
                        item.Code,
                        item.Label,
                        item.Guidance,
                        item.ResponseType.ToString(),
                        item.IsRequired,
                        item.RequiresEvidenceOnIssue,
                        item.SortOrder,
                        item.IsCompleted,
                        item.BooleanValue,
                        item.NumberValue,
                        item.TextValue,
                        item.EvidenceAttachmentId)).ToList());
            })
            .Where(stage => stage.Items.Count > 0)
            .ToList();
        var required = items.Where(item => item.IsRequired).ToList();
        return new ProviderMissionQualityChecklistResponse(
            control.Id,
            control.Status.ToString(),
            required.Count,
            required.Count(item => item.IsCompleted),
            items.Where(item => item.IsRequired && item.Stage == QualityChecklistStage.BeforeStart).All(item => item.IsCompleted),
            required.All(item => item.IsCompleted),
            stages);
    }

    private static ProviderMissionQualityChecklistResponse EmptyChecklist() =>
        new(null, "NotConfigured", 0, 0, true, true, []);

    private static string StageLabel(QualityChecklistStage stage) => stage switch
    {
        QualityChecklistStage.BeforeStart => "Avant l'intervention",
        QualityChecklistStage.DuringMission => "Intervention",
        QualityChecklistStage.BeforeCompletion => "Controle final",
        _ => stage.ToString()
    };
}

public sealed record QualityGateResult(bool IsAllowed, string? Message, IReadOnlyList<string> MissingItems)
{
    public static QualityGateResult Allowed() => new(true, null, []);
    public static QualityGateResult Blocked(string message, IReadOnlyList<string> missingItems) => new(false, message, missingItems);
}

public sealed record QualityChecklistOperationResult(
    bool IsSuccess,
    bool IsNotFound,
    string? Message,
    ProviderMissionQualityChecklistResponse? Checklist)
{
    public static QualityChecklistOperationResult Ok(ProviderMissionQualityChecklistResponse checklist) => new(true, false, null, checklist);
    public static QualityChecklistOperationResult NotFound(string message) => new(false, true, message, null);
    public static QualityChecklistOperationResult Invalid(string message) => new(false, false, message, null);
}
