namespace HomeService.Application.Admin;

public enum AdminCompanyApplicationReviewStatus
{
    Ok = 0,
    NotFound = 1,
    ValidationFailed = 2,
    MissingRequiredApprovedDocuments = 3,
    InvalidTransition = 4
}
