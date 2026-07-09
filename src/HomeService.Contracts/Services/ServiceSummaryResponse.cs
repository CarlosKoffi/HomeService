namespace HomeService.Contracts.Services;

public sealed record ServiceSummaryResponse(
    Guid Id,
    string Name,
    string? Description,
    string Status,
    bool IsActive);
