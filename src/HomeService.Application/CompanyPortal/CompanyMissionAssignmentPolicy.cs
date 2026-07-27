namespace HomeService.Application.CompanyPortal;

public static class CompanyMissionAssignmentPolicy
{
    public static CompanyMissionAssignmentPolicyResult Validate(
        bool missionExists,
        bool providerExists,
        bool providerIsApproved,
        bool providerCoversMissionService,
        bool providerHasBlockingAssignment,
        bool providerAlreadyUnavailableForMission = false)
    {
        if (!missionExists)
        {
            return CompanyMissionAssignmentPolicyResult.NotFound("Mission introuvable.");
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

        return CompanyMissionAssignmentPolicyResult.Ok();
    }
}

public sealed record CompanyMissionAssignmentPolicyResult(bool IsValid, bool IsNotFound, string? Message)
{
    public static CompanyMissionAssignmentPolicyResult Ok() => new(true, false, null);
    public static CompanyMissionAssignmentPolicyResult Invalid(string message) => new(false, false, message);
    public static CompanyMissionAssignmentPolicyResult NotFound(string message) => new(false, true, message);
}
