using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Application.Admin;

public sealed record AdminCompanyApplicationDocumentReviewResult(
    AdminCompanyApplicationDocumentReviewStatus Status,
    CompanyApplicationDocument? Document,
    DocumentReviewStatus? PreviousStatus,
    string? Message)
{
    public static AdminCompanyApplicationDocumentReviewResult Ok(CompanyApplicationDocument document, DocumentReviewStatus previousStatus)
        => new(AdminCompanyApplicationDocumentReviewStatus.Ok, document, previousStatus, null);

    public static AdminCompanyApplicationDocumentReviewResult NotFound()
        => new(AdminCompanyApplicationDocumentReviewStatus.NotFound, null, null, null);

    public static AdminCompanyApplicationDocumentReviewResult ValidationFailed(string message)
        => new(AdminCompanyApplicationDocumentReviewStatus.ValidationFailed, null, null, message);

    public static AdminCompanyApplicationDocumentReviewResult InvalidTransition(string message)
        => new(AdminCompanyApplicationDocumentReviewStatus.InvalidTransition, null, null, message);
}
