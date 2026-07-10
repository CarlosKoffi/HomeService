namespace HomeService.Contracts.Companies;

public sealed record CompanyApplicationActionResponse(
    Guid Id,
    string Status,
    DateTimeOffset? ReviewedAt,
    string? ReviewNote);
