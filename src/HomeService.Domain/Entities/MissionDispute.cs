using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class MissionDispute : AuditableEntity
{
    private MissionDispute()
    {
    }

    public MissionDispute(
        Guid missionId,
        MissionCancellationActor openedBy,
        MissionCancellationReason reason,
        string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("A dispute description is required.", nameof(description));
        }

        MissionId = missionId;
        OpenedBy = openedBy;
        Reason = reason;
        Description = description.Trim();
        OpenedAt = DateTimeOffset.UtcNow;
    }

    public Guid MissionId { get; private set; }
    public Mission? Mission { get; private set; }
    public MissionDisputeStatus Status { get; private set; } = MissionDisputeStatus.Open;
    public MissionCancellationActor OpenedBy { get; private set; }
    public MissionCancellationReason Reason { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public MissionDisputeResolution? Resolution { get; private set; }
    public string? ResolutionNote { get; private set; }
    public DateTimeOffset OpenedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    public void Resolve(MissionDisputeResolution resolution, string note)
    {
        if (Status == MissionDisputeStatus.Resolved)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(note))
        {
            throw new InvalidOperationException("Resolution note is required.");
        }

        Status = MissionDisputeStatus.Resolved;
        Resolution = resolution;
        ResolutionNote = note.Trim();
        ResolvedAt = DateTimeOffset.UtcNow;
        Touch();
    }
}
