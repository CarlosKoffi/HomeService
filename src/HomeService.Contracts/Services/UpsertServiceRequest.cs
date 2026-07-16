namespace HomeService.Contracts.Services;

public sealed record UpsertServiceRequest(
    string Name,
    string? Description,
    string? IconName,
    int NormalPriceAmount = 0,
    int PremiumPriceAmount = 0,
    string Currency = "XOF",
    int? PriceMinAmount = null,
    int? PriceMaxAmount = null);
