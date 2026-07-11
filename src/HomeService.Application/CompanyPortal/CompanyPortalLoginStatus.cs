namespace HomeService.Application.CompanyPortal;

public enum CompanyPortalLoginStatus
{
    Ok = 0,
    MissingCredentials = 1,
    InvalidCredentials = 2,
    CompanySuspended = 3
}
