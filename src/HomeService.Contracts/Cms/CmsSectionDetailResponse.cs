namespace HomeService.Contracts.Cms;

public sealed record CmsSectionDetailResponse(
    Guid Id,
    Guid PageVersionId,
    string ComponentKey,
    string ComponentName,
    string InternalName,
    string Zone,
    int Position,
    string? Anchor,
    string? Variant,
    bool IsActive,
    IReadOnlyList<CmsContentValueResponse> ContentValues);
