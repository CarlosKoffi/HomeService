namespace HomeService.Contracts.Companies;

public sealed record CompanyApplicationDocumentResponse(
    Guid Id,
    string DocumentType,
    string OriginalFileName,
    string ContentType,
    string ReviewStatus,
    string? ReviewNote,
    DateTimeOffset CreatedAt);
