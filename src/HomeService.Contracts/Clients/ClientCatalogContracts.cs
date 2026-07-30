namespace HomeService.Contracts.Clients;

public sealed record ClientCatalogSearchResultResponse(
    Guid Id,
    string Type,
    string Name,
    string? Description,
    Guid ServiceId,
    string ServiceName,
    Guid? PrestationId,
    string? PrestationName,
    int? PriceMinAmount,
    int? PriceMaxAmount,
    string Currency,
    string IconName,
    string? IconUrl = null,
    string? ImageUrl = null,
    bool RequiresPhoto = false);
