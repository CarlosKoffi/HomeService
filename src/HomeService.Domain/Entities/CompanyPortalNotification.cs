using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class CompanyPortalNotification : AuditableEntity
{
    private CompanyPortalNotification()
    {
    }

    public CompanyPortalNotification(
        Guid companyId,
        Guid? companyApplicationId,
        Guid? companyApplicationDocumentId,
        string type,
        string title,
        string message,
        string tone,
        string? actionUrl = null)
    {
        CompanyId = companyId;
        CompanyApplicationId = companyApplicationId;
        CompanyApplicationDocumentId = companyApplicationDocumentId;
        Type = CleanRequired(type);
        Title = CleanRequired(title);
        Message = CleanRequired(message);
        Tone = CleanRequired(tone);
        ActionUrl = Clean(actionUrl);
    }

    public Guid CompanyId { get; private set; }
    public Company? Company { get; private set; }
    public Guid? CompanyApplicationId { get; private set; }
    public CompanyApplication? CompanyApplication { get; private set; }
    public Guid? CompanyApplicationDocumentId { get; private set; }
    public CompanyApplicationDocument? CompanyApplicationDocument { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string Tone { get; private set; } = string.Empty;
    public string? ActionUrl { get; private set; }
    public bool IsRead { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; } = DateTimeOffset.UtcNow;

    public void MarkRead()
    {
        if (IsRead)
        {
            return;
        }

        IsRead = true;
        Touch();
    }

    public void MarkUnread()
    {
        if (!IsRead)
        {
            return;
        }

        IsRead = false;
        Touch();
    }

    private static string CleanRequired(string value)
    {
        var cleaned = Clean(value);
        if (cleaned is null)
        {
            throw new ArgumentException("La valeur obligatoire est vide.", nameof(value));
        }

        return cleaned;
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
