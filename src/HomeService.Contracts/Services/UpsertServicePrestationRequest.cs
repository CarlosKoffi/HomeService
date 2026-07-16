namespace HomeService.Contracts.Services;

public sealed record UpsertServicePrestationRequest(
    string Name,
    string? Description,
    int SortOrder,
    int NormalPriceAmount = 0,
    int PremiumPriceAmount = 0,
    string Currency = "XOF",
    int? PriceMinAmount = null,
    int? PriceMaxAmount = null);
