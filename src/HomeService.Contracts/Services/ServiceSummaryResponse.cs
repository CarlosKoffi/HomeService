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
    string Currency);
