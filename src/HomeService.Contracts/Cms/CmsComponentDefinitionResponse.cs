namespace HomeService.Contracts.Cms;

public sealed record CmsComponentDefinitionResponse(
    Guid Id,
    string Key,
    string Name,
    string? Description,
    int SchemaVersion,
    bool IsActive);
