namespace HomeService.Application.Companies;

public sealed record CompanyApplicationRegistrationPlan(
    string Email,
    IReadOnlyList<string> ServiceNames,
    IReadOnlyList<string> ValidationErrors);
