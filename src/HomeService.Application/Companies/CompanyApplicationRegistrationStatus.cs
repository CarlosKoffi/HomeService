namespace HomeService.Application.Companies;

public enum CompanyApplicationRegistrationStatus
{
    Created = 0,
    ValidationFailed = 1,
    DuplicateEmail = 2
}
