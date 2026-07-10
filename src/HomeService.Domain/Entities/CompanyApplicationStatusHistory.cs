using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class CompanyApplicationStatusHistory : AuditableEntity
{
    private CompanyApplicationStatusHistory()
    {
    }

    internal CompanyApplicationStatusHistory(
        Guid companyApplicationId,
        CompanyApplicationStatus? previousStatus,
        CompanyApplicationStatus newStatus,
        string? note,
        string? changedBy)
    {
        CompanyApplicationId = companyApplicationId;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        Note = note?.Trim();
        ChangedBy = changedBy?.Trim();
        ChangedAt = DateTimeOffset.UtcNow;
    }

    public Guid CompanyApplicationId { get; private set; }
    public CompanyApplication? CompanyApplication { get; private set; }
    public CompanyApplicationStatus? PreviousStatus { get; private set; }
    public CompanyApplicationStatus NewStatus { get; private set; }
    public string? Note { get; private set; }
    public string? ChangedBy { get; private set; }
    public DateTimeOffset ChangedAt { get; private set; }
}
