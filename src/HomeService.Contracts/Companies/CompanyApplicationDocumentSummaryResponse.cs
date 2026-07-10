namespace HomeService.Contracts.Companies;

public sealed record CompanyApplicationDocumentSummaryResponse(
    Guid Id,
    string DocumentType,
    string ReviewStatus,
    string? ReviewNote);
