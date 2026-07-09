namespace HomeService.Domain.Enums;

public enum CompanyApplicationStatus
{
    Draft = 0,
    Submitted = 1,
    UnderReview = 2,
    MoreInformationRequested = 3,
    Approved = 4,
    Rejected = 5,
    ActivationSent = 6,
    Activated = 7
}
