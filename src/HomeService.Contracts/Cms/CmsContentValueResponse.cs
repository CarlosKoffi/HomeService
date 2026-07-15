namespace HomeService.Contracts.Cms;

public sealed record CmsContentValueResponse(
    Guid Id,
    Guid SectionId,
    string FieldKey,
    string ValueType,
    string? LanguageCode,
    string? TextValue,
    string? JsonValue,
    Guid? MediaAssetId,
    string? MediaUrl);
