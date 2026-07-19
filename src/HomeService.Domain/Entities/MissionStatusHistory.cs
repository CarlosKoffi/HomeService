using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class MissionStatusHistory : AuditableEntity
{
    private MissionStatusHistory()
    {
    }

    public MissionStatusHistory(
        Guid missionId,
        MissionStatus fromStatus,
        MissionStatus toStatus,
        string actorType,
        Guid? actorId,
        string? note)
    {
        MissionId = missionId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ActorType = actorType.Trim();
        ActorId = actorId;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public Guid MissionId { get; private set; }
    public Mission? Mission { get; private set; }
    public MissionStatus FromStatus { get; private set; }
    public MissionStatus ToStatus { get; private set; }
    public string ActorType { get; private set; } = string.Empty;
    public Guid? ActorId { get; private set; }
    public string? Note { get; private set; }
}
