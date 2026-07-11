namespace HomeService.Application.Companies;

public enum CompanyActivationPasswordStatus
{
    Ok = 0,
    ValidationFailed = 1,
    InvalidOrExpiredToken = 2,
    DuplicatePortalUser = 3
}
