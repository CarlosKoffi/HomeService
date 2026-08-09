using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class MissionQualityControl : AuditableEntity
{
    private readonly List<MissionQualityItem> _items = [];

    private MissionQualityControl()
    {
    }

    public MissionQualityControl(Guid missionId, Guid templateId, int templateVersion)
    {
        MissionId = missionId;
        TemplateId = templateId;
        TemplateVersion = Math.Max(1, templateVersion);
    }

    public Guid MissionId { get; private set; }
    public Mission? Mission { get; private set; }
    public Guid TemplateId { get; private set; }
    public QualityChecklistTemplate? Template { get; private set; }
    public int TemplateVersion { get; private set; }
    public MissionQualityControlStatus Status { get; private set; } = MissionQualityControlStatus.Pending;
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? LockedAt { get; private set; }
    public IReadOnlyCollection<MissionQualityItem> Items => _items;

    public void MarkInProgress()
    {
        if (Status == MissionQualityControlStatus.Pending)
        {
            Status = MissionQualityControlStatus.InProgress;
            StartedAt = DateTimeOffset.UtcNow;
            Touch();
        }
    }

    public void MarkCompleted()
    {
        if (_items.Any(item => item.IsRequired && !item.IsCompleted))
        {
            throw new InvalidOperationException("Tous les controles obligatoires doivent etre termines.");
        }

        Status = MissionQualityControlStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    public void Lock()
    {
        Status = MissionQualityControlStatus.Locked;
        LockedAt = DateTimeOffset.UtcNow;
        Touch();
    }
}
