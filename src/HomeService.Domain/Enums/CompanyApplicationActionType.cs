namespace HomeService.Domain.Enums;

public enum CompanyApplicationActionType
{
    Submitted = 0,
    DocumentApproved = 1,
    DocumentReplacementRequested = 2,
    MoreInformationRequested = 3,
    Approved = 4,
    Rejected = 5,
    ActivationLinkSent = 6,
    ActivationLinkRegenerated = 7,
    Activated = 8
}
