using HomeService.Domain.Enums;

namespace HomeService.Application.Companies;

public sealed record CompanyApplicationUploadedDocument(
    CompanyDocumentType DocumentType,
    string OriginalFileName,
    string StoragePath,
    string ContentType);
