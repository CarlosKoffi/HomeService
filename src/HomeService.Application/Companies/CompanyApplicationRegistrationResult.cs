using HomeService.Domain.Entities;

namespace HomeService.Application.Companies;

public sealed record CompanyApplicationRegistrationResult(
    CompanyApplicationRegistrationStatus Status,
    CompanyApplication? Application,
    Company? Company,
    IReadOnlyList<string> Errors,
    string? Message,
    int ServiceCount,
    int DocumentCount)
{
    public static CompanyApplicationRegistrationResult ValidationFailed(IReadOnlyList<string> errors)
        => new(CompanyApplicationRegistrationStatus.ValidationFailed, null, null, errors, "Le formulaire contient des erreurs.", 0, 0);

    public static CompanyApplicationRegistrationResult DuplicateEmail(string message)
        => new(CompanyApplicationRegistrationStatus.DuplicateEmail, null, null, [], message, 0, 0);

    public static CompanyApplicationRegistrationResult Created(CompanyApplication application, Company company, int serviceCount, int documentCount)
        => new(CompanyApplicationRegistrationStatus.Created, application, company, [], null, serviceCount, documentCount);
}
