namespace HomeService.Contracts.Localization;

public sealed record AdminTranslationListResponse(
    IReadOnlyList<AdminTranslationResponse> Items,
    IReadOnlyList<string> Scopes,
    IReadOnlyList<string> Languages);

public sealed record AdminTranslationResponse(
    Guid KeyId,
    Guid? ValueId,
    string Key,
    string Scope,
    string Description,
    string Language,
    string Value,
    bool IsActive);

public sealed record UpsertAdminTranslationRequest(
    string Key,
    string Scope,
    string Description,
    string Language,
    string Value);
