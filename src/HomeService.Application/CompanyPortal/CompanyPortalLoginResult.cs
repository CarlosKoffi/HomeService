using HomeService.Contracts.CompanyPortal;
using HomeService.Domain.Entities;

namespace HomeService.Application.CompanyPortal;

public sealed record CompanyPortalLoginResult(
    CompanyPortalLoginStatus Status,
    CompanyPortalLoginResponse? Response,
    CompanyPortalSession? Session,
    Company? Company,
    string? Message)
{
    public static CompanyPortalLoginResult Ok(
        CompanyPortalLoginResponse response,
        CompanyPortalSession session,
        Company company)
        => new(CompanyPortalLoginStatus.Ok, response, session, company, null);

    public static CompanyPortalLoginResult MissingCredentials()
        => new(CompanyPortalLoginStatus.MissingCredentials, null, null, null, "Email et mot de passe sont obligatoires.");

    public static CompanyPortalLoginResult InvalidCredentials()
        => new(CompanyPortalLoginStatus.InvalidCredentials, null, null, null, null);

    public static CompanyPortalLoginResult Suspended()
        => new(CompanyPortalLoginStatus.CompanySuspended, null, null, null, "Cette entreprise est suspendue. Contactez le support.");
}
