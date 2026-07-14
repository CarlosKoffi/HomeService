namespace HomeService.Contracts.Cms;

public sealed record CmsMenuSummaryResponse(
    Guid Id,
    string Code,
    string Name,
    string Placement,
    bool IsActive,
    int ItemCount);
