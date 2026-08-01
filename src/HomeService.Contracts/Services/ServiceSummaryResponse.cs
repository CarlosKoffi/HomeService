namespace HomeService.Contracts.Services;

public sealed record ServiceSummaryResponse(
    Guid Id,
    string Name,
    string? Description,
    string IconName,
    string Status,
    bool IsActive,
    int NormalPriceAmount,
    int PremiumPriceAmount,
    string Currency,
    IReadOnlyList<ServicePrestationSummaryResponse> Prestations,
    int? PriceMinAmount = null,
    int? PriceMaxAmount = null,
    string? IconUrl = null,
    string? ImageUrl = null,
    string DisplayCategory = "Home",
    bool IsFixedPrice = false);

public sealed record ServicePrestationSummaryResponse(
    Guid Id,
    string Name,
    string? Description,
    int SortOrder,
    int NormalPriceAmount,
    int PremiumPriceAmount,
    string Currency,
    bool IsActive,
    int? PriceMinAmount = null,
    int? PriceMaxAmount = null,
    string? IllustrationUrl = null,
    int MissionCount = 0,
    bool IsFixedPrice = false,
    IReadOnlyList<ServiceOptionSummaryResponse>? Options = null);

public sealed record ServiceOptionSummaryResponse(
    Guid Id,
    Guid ServicePrestationId,
    string Name,
    string? Description,
    int SortOrder,
    int PriceMinAmount,
    int PriceMaxAmount,
    bool IsFixedPrice,
    string Currency,
    bool IsActive);
