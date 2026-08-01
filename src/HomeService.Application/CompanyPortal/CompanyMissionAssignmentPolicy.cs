namespace HomeService.Application.CompanyPortal;

using HomeService.Domain.Enums;

public static class CompanyMissionAssignmentPolicy
{
    public static CompanyMissionAssignmentPolicyResult Validate(
        bool missionExists,
        bool providerExists,
        bool providerIsApproved,
        bool providerCoversMissionService,
        bool providerHasBlockingAssignment,
        bool providerAlreadyUnavailableForMission = false,
        MissionStatus? missionStatus = null,
        bool providerIsAvailable = true)
    {
        if (!missionExists)
        {
            return CompanyMissionAssignmentPolicyResult.NotFound("Mission introuvable.");
        }

        if (missionStatus is not null && missionStatus != MissionStatus.SearchingProvider)
        {
            return CompanyMissionAssignmentPolicyResult.Invalid("Cette mission n'est plus affectable dans son etat actuel.");
        }

        if (!providerExists || !providerIsApproved)
        {
            return CompanyMissionAssignmentPolicyResult.NotFound("Prestataire introuvable ou non valide.");
        }

        if (!providerCoversMissionService)
        {
            return CompanyMissionAssignmentPolicyResult.Invalid("Ce prestataire ne couvre pas le service de la mission.");
        }

        if (providerHasBlockingAssignment)
        {
            return CompanyMissionAssignmentPolicyResult.Invalid("Ce prestataire a deja une mission en attente ou en cours.");
        }

        if (providerAlreadyUnavailableForMission)
        {
            return CompanyMissionAssignmentPolicyResult.Invalid("Ce prestataire a deja refuse cette mission ou depasse le delai de reponse.");
        }

        if (!providerIsAvailable)
        {
            return CompanyMissionAssignmentPolicyResult.Invalid("Ce prestataire est actuellement indisponible.");
        }

        return CompanyMissionAssignmentPolicyResult.Ok();
    }
}

public sealed record CompanyMissionAssignmentPolicyResult(bool IsValid, bool IsNotFound, string? Message)
{
    public static CompanyMissionAssignmentPolicyResult Ok() => new(true, false, null);
    public static CompanyMissionAssignmentPolicyResult Invalid(string message) => new(false, false, message);
    public static CompanyMissionAssignmentPolicyResult NotFound(string message) => new(false, true, message);
}
