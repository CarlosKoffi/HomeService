using HomeService.Contracts.Companies;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Application.Companies;

public sealed record CompanyActivationPasswordResult(
    CompanyActivationPasswordStatus Status,
    CompanyActivationPasswordResponse? Response,
    CompanyApplication? Application,
    Company? Company,
    CompanyApplicationStatus? PreviousStatus,
    string? Email,
    string? Message)
{
    public static CompanyActivationPasswordResult Ok(
        CompanyActivationPasswordResponse response,
        CompanyApplication application,
        Company company,
        CompanyApplicationStatus previousStatus,
        string email)
        => new(CompanyActivationPasswordStatus.Ok, response, application, company, previousStatus, email, null);

    public static CompanyActivationPasswordResult ValidationFailed(string message)
        => new(CompanyActivationPasswordStatus.ValidationFailed, null, null, null, null, null, message);

    public static CompanyActivationPasswordResult InvalidOrExpiredToken()
        => new(CompanyActivationPasswordStatus.InvalidOrExpiredToken, null, null, null, null, null, "Le lien d'activation est invalide ou expire.");

    public static CompanyActivationPasswordResult DuplicatePortalUser()
        => new(CompanyActivationPasswordStatus.DuplicatePortalUser, null, null, null, null, null, "Un compte portail existe deja pour cet email.");
}
