using HomeService.Contracts.Companies;
using HomeService.Domain.Enums;

namespace HomeService.Application.Companies;

public sealed record CompanyActivationLinkGenerationResult(
    CompanyActivationLinkGenerationStatus Status,
    CompanyApplicationActivationLinkResponse? Response,
    CompanyApplicationStatus? PreviousStatus,
    string? Message)
{
    public static CompanyActivationLinkGenerationResult NotFound()
        => new(CompanyActivationLinkGenerationStatus.NotFound, null, null, "La demande entreprise n'existe plus.");

    public static CompanyActivationLinkGenerationResult InvalidStatus()
        => new(CompanyActivationLinkGenerationStatus.InvalidStatus, null, null, "Le lien d'activation ne peut etre genere qu'apres validation du dossier.");

    public static CompanyActivationLinkGenerationResult Ok(
        CompanyApplicationActivationLinkResponse response,
        CompanyApplicationStatus previousStatus)
        => new(CompanyActivationLinkGenerationStatus.Ok, response, previousStatus, null);
}
