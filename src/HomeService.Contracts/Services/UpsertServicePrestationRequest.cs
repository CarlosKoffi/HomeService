namespace HomeService.Contracts.Services;

public sealed record UpsertServicePrestationRequest(
    string Name,
    string? Description,
    int SortOrder);
