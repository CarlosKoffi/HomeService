using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class MissionQualityItem : AuditableEntity
{
    private MissionQualityItem()
    {
    }

    public MissionQualityItem(Guid controlId, QualityChecklistItem source)
    {
        ControlId = controlId;
        TemplateItemId = source.Id;
        Code = source.Code;
        Label = source.Label;
        Guidance = source.Guidance;
        Stage = source.Stage;
        ResponseType = source.ResponseType;
        IsRequired = source.IsRequired;
        RequiresEvidenceOnIssue = source.RequiresEvidenceOnIssue;
        SortOrder = source.SortOrder;
    }

    public Guid ControlId { get; private set; }
    public MissionQualityControl? Control { get; private set; }
    public Guid? TemplateItemId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public string? Guidance { get; private set; }
    public QualityChecklistStage Stage { get; private set; }
    public QualityChecklistResponseType ResponseType { get; private set; }
    public bool IsRequired { get; private set; }
    public bool RequiresEvidenceOnIssue { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsCompleted { get; private set; }
    public bool? BooleanValue { get; private set; }
    public decimal? NumberValue { get; private set; }
    public string? TextValue { get; private set; }
    public Guid? EvidenceAttachmentId { get; private set; }
    public MissionAttachment? EvidenceAttachment { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public void Respond(bool? booleanValue, decimal? numberValue, string? textValue, Guid? evidenceAttachmentId)
    {
        BooleanValue = booleanValue;
        NumberValue = numberValue;
        TextValue = Clean(textValue, 1200);
        EvidenceAttachmentId = evidenceAttachmentId;
        IsCompleted = IsResponseComplete();
        CompletedAt = IsCompleted ? DateTimeOffset.UtcNow : null;
        Touch();
    }

    private bool IsResponseComplete() => ResponseType switch
    {
        QualityChecklistResponseType.Automatic => BooleanValue == true,
        QualityChecklistResponseType.Confirmation => BooleanValue == true,
        QualityChecklistResponseType.YesNo => BooleanValue.HasValue,
        QualityChecklistResponseType.Photo => EvidenceAttachmentId.HasValue,
        QualityChecklistResponseType.ShortText => !string.IsNullOrWhiteSpace(TextValue),
        QualityChecklistResponseType.Number => NumberValue.HasValue,
        QualityChecklistResponseType.Choice => !string.IsNullOrWhiteSpace(TextValue),
        _ => false
    };

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }
}
