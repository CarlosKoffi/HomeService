using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class CmsContentValue : AuditableEntity
{
    private CmsContentValue()
    {
    }

    public CmsContentValue(Guid sectionId, string fieldKey, CmsContentValueType valueType, Guid? languageId = null)
    {
        SectionId = sectionId;
        FieldKey = NormalizeKey(fieldKey);
        ValueType = valueType;
        LanguageId = languageId;
    }

    public Guid SectionId { get; private set; }
    public CmsSection? Section { get; private set; }
    public string FieldKey { get; private set; } = string.Empty;
    public CmsContentValueType ValueType { get; private set; }
    public Guid? LanguageId { get; private set; }
    public Language? Language { get; private set; }
    public string? TextValue { get; private set; }
    public decimal? DecimalValue { get; private set; }
    public bool? BooleanValue { get; private set; }
    public DateTimeOffset? DateTimeValue { get; private set; }
    public Guid? MediaAssetId { get; private set; }
    public CmsMediaAsset? MediaAsset { get; private set; }
    public string? JsonValue { get; private set; }

    public void SetText(string? value)
    {
        TextValue = value?.Trim();
        Touch();
    }

    public void SetJson(string? value)
    {
        JsonValue = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        Touch();
    }

    public void AttachMedia(Guid mediaAssetId, string? publicUrl)
    {
        MediaAssetId = mediaAssetId;
        TextValue = string.IsNullOrWhiteSpace(publicUrl) ? null : publicUrl.Trim();
        Touch();
    }

    private static string NormalizeKey(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A CMS content field key is required.", nameof(value))
            : value.Trim();
    }
}
