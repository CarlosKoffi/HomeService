namespace HomeService.Contracts.CompanyPortal;

public sealed record CompanyEmployeeDocumentResponse(
    Guid Id,
    string DocumentType,
    string OriginalFileName,
    string ContentType,
    string PreviewUrl,
    DateTimeOffset CreatedAt);
