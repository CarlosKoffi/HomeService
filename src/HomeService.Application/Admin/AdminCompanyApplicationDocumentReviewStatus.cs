namespace HomeService.Application.Admin;

public enum AdminCompanyApplicationDocumentReviewStatus
{
    Ok = 0,
    NotFound = 1,
    ValidationFailed = 2,
    InvalidTransition = 3
}
