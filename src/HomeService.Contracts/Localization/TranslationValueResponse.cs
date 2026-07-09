namespace HomeService.Contracts.Localization;

public sealed record TranslationValueResponse(
    string Key,
    string Scope,
    string Value);
