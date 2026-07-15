namespace HomeService.Contracts.Cms;

public sealed record CmsPageDetailResponse(
    Guid Id,
    Guid SiteId,
    string Code,
    string InternalName,
    string TemplateKey,
    string Status,
    string? DefaultTitle,
    string? DefaultSlug,
    int VersionNumber,
    string VersionStatus,
    IReadOnlyList<CmsSectionDetailResponse> Sections);
