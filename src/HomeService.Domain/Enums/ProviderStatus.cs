namespace HomeService.Domain.Enums;

public enum ProviderStatus
{
    Invited = 0,
    ProfileIncomplete = 1,
    PendingPlatformReview = 2,
    Approved = 3,
    SuspendedByCompany = 4,
    SuspendedByPlatform = 5,
    Inactive = 6
}
