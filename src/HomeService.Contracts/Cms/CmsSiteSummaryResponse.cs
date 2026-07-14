namespace HomeService.Contracts.Cms;

public sealed record CmsSiteSummaryResponse(
    Guid Id,
    string Code,
    string Name,
    string Surface,
    string Status,
    string? DefaultCountryIsoCode,
    string DefaultLanguageCode,
    string? HomePageCode,
    int PageCount,
    int MenuCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
