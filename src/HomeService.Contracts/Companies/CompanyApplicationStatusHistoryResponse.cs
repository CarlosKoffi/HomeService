namespace HomeService.Contracts.Companies;

public sealed record CompanyApplicationStatusHistoryResponse(
    Guid Id,
    string? PreviousStatus,
    string NewStatus,
    string? Note,
    string? ChangedBy,
    DateTimeOffset ChangedAt);
