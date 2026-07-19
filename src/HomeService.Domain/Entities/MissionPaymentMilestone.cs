using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class MissionPaymentMilestone : AuditableEntity
{
    private MissionPaymentMilestone()
    {
    }

    public MissionPaymentMilestone(
        Guid missionId,
        MissionPaymentMilestoneTrigger trigger,
        int amount,
        string currency,
        string label,
        int sortOrder)
    {
        MissionId = missionId;
        Trigger = trigger;
        Amount = Math.Max(0, amount);
        Currency = string.IsNullOrWhiteSpace(currency) ? "XOF" : currency.Trim().ToUpperInvariant();
        Label = label.Trim();
        SortOrder = Math.Max(0, sortOrder);
    }

    public Guid MissionId { get; private set; }
    public Mission? Mission { get; private set; }
    public MissionPaymentMilestoneTrigger Trigger { get; private set; }
    public MissionPaymentMilestoneStatus Status { get; private set; } = MissionPaymentMilestoneStatus.Pending;
    public int Amount { get; private set; }
    public string Currency { get; private set; } = "XOF";
    public string Label { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public DateTimeOffset? DueAt { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }
    public string? ExternalPaymentReference { get; private set; }

    public void MarkDue(DateTimeOffset dueAt)
    {
        DueAt = dueAt;
        Touch();
    }

    public void MarkPaid(string? externalPaymentReference)
    {
        Status = MissionPaymentMilestoneStatus.Paid;
        PaidAt = DateTimeOffset.UtcNow;
        ExternalPaymentReference = string.IsNullOrWhiteSpace(externalPaymentReference) ? null : externalPaymentReference.Trim();
        Touch();
    }

    public void MarkFailed()
    {
        Status = MissionPaymentMilestoneStatus.Failed;
        Touch();
    }

    public void Cancel()
    {
        Status = MissionPaymentMilestoneStatus.Cancelled;
        Touch();
    }
}
