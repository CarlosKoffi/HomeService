namespace HomeService.Contracts.Cms;

public sealed record UpdateCmsContentValueRequest(
    string? TextValue,
    string? JsonValue);
