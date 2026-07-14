namespace HomeService.Contracts.Cms;

public sealed record CmsSiteDetailResponse(
    Guid Id,
    string Code,
    string Name,
    string Surface,
    string Status,
    string? DefaultCountryIsoCode,
    string DefaultLanguageCode,
    string? HomePageCode,
    IReadOnlyList<CmsPageSummaryResponse> Pages,
    IReadOnlyList<CmsMenuSummaryResponse> Menus);
