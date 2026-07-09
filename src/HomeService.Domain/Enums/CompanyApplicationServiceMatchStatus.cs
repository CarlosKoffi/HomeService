namespace HomeService.Domain.Enums;

public enum CompanyApplicationServiceMatchStatus
{
    PendingMatch = 0,
    MatchedExisting = 1,
    NeedsAdminReview = 2,
    CreatedAsNewService = 3,
    Rejected = 4
}
