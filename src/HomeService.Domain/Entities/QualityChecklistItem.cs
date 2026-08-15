using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class QualityChecklistItem : AuditableEntity
{
    private QualityChecklistItem()
    {
    }

    public QualityChecklistItem(
        Guid templateId,
        string code,
        string label,
        QualityChecklistStage stage,
        QualityChecklistResponseType responseType,
        bool isRequired,
        int sortOrder,
        string? guidance = null,
        Guid? serviceOptionId = null,
        bool requiresEvidenceOnIssue = false)
    {
        TemplateId = templateId;
        Code = CleanRequired(code, 80).ToLowerInvariant();
        Label = CleanRequired(label, 240);
        Stage = stage;
        ResponseType = responseType;
        IsRequired = isRequired;
        SortOrder = Math.Max(0, sortOrder);
        Guidance = Clean(guidance, 600);
        ServiceOptionId = serviceOptionId;
        RequiresEvidenceOnIssue = requiresEvidenceOnIssue;
    }

    public Guid TemplateId { get; private set; }
    public QualityChecklistTemplate? Template { get; private set; }
    public Guid? ServiceOptionId { get; private set; }
    public ServiceOption? ServiceOption { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public string? Guidance { get; private set; }
    public QualityChecklistStage Stage { get; private set; }
    public QualityChecklistResponseType ResponseType { get; private set; }
    public bool IsRequired { get; private set; }
    public bool RequiresEvidenceOnIssue { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    public void Update(
        string label,
        string? guidance,
        QualityChecklistStage stage,
        QualityChecklistResponseType responseType,
        bool isRequired,
        bool requiresEvidenceOnIssue,
        int sortOrder,
        Guid? serviceOptionId = null)
    {
        Label = CleanRequired(label, 240);
        Guidance = Clean(guidance, 600);
        Stage = stage;
        ResponseType = responseType;
        IsRequired = isRequired;
        RequiresEvidenceOnIssue = requiresEvidenceOnIssue;
        SortOrder = Math.Max(0, sortOrder);
        ServiceOptionId = serviceOptionId;
        Touch();
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        Touch();
    }

    private static string CleanRequired(string value, int maxLength)
    {
        var cleaned = value?.Trim() ?? string.Empty;
        if (cleaned.Length == 0) throw new ArgumentException("Une valeur est obligatoire.", nameof(value));
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }
}
