using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Application.Admin;

public sealed record AdminCompanyApplicationReviewResult(
    AdminCompanyApplicationReviewStatus Status,
    CompanyApplication? Application,
    CompanyApplicationStatus? PreviousStatus,
    string? Message)
{
    public static AdminCompanyApplicationReviewResult Ok(CompanyApplication application, CompanyApplicationStatus previousStatus)
        => new(AdminCompanyApplicationReviewStatus.Ok, application, previousStatus, null);

    public static AdminCompanyApplicationReviewResult NotFound()
        => new(AdminCompanyApplicationReviewStatus.NotFound, null, null, null);

    public static AdminCompanyApplicationReviewResult ValidationFailed(string message)
        => new(AdminCompanyApplicationReviewStatus.ValidationFailed, null, null, message);

    public static AdminCompanyApplicationReviewResult MissingRequiredDocuments(string message)
        => new(AdminCompanyApplicationReviewStatus.MissingRequiredApprovedDocuments, null, null, message);

    public static AdminCompanyApplicationReviewResult InvalidTransition(string message)
        => new(AdminCompanyApplicationReviewStatus.InvalidTransition, null, null, message);
}
