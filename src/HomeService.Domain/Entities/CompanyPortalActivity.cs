using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class CompanyPortalActivity : AuditableEntity
{
    private CompanyPortalActivity()
    {
    }

    public CompanyPortalActivity(
        Guid companyId,
        string type,
        string title,
        string description,
        string tone,
        string? entityType = null,
        Guid? entityId = null)
    {
        CompanyId = companyId;
        Type = type.Trim();
        Title = title.Trim();
        Description = description.Trim();
        Tone = tone.Trim();
        EntityType = entityType?.Trim();
        EntityId = entityId;
        OccurredAt = DateTimeOffset.UtcNow;
    }

    public Guid CompanyId { get; private set; }
    public Company? Company { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Tone { get; private set; } = "blue";
    public string? EntityType { get; private set; }
    public Guid? EntityId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public bool IsRead { get; private set; }

    public void MarkAsRead()
    {
        IsRead = true;
        Touch();
    }
}
