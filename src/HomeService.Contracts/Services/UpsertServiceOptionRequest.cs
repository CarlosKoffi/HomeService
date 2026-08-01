namespace HomeService.Contracts.Services;

public sealed record UpsertServiceOptionRequest(
    string Name,
    string? Description,
    int SortOrder,
    int PriceMinAmount,
    int PriceMaxAmount,
    bool IsFixedPrice = false,
    string Currency = "XOF");
