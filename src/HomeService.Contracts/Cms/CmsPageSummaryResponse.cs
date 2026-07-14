namespace HomeService.Contracts.Cms;

public sealed record CmsPageSummaryResponse(
    Guid Id,
    Guid SiteId,
    string Code,
    string InternalName,
    string TemplateKey,
    string Status,
    bool RequiresAuthentication,
    string? DefaultTitle,
    string? DefaultSlug,
    int VersionCount,
    int SectionCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
