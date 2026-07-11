using HomeService.Contracts.ProviderPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Application.ProviderPortal;

public sealed class ProviderMissionWorkflowService
{
    public ProviderMissionOperationResult AcceptMission(
        ProviderProfile provider,
        ProviderMissionAssignment assignment,
        ProviderAcceptMissionRequest request)
    {
        if (!CanProviderUsePortal(provider))
        {
            return ProviderMissionOperationResult.Forbidden("Ce prestataire n'est pas autorise a utiliser le portail.");
        }

        var locationRequest = new ProviderLocationVerificationRequest(request.Latitude, request.Longitude, request.AccuracyMeters);
        if (ProviderLocationPayloadValidator.Validate(locationRequest) is { } validationError)
        {
            return ProviderMissionOperationResult.BadRequest(validationError);
        }

        if (assignment.Mission is null)
        {
            return ProviderMissionOperationResult.NotFound("Mission introuvable pour ce prestataire.");
        }

        if (assignment.Status == ProviderMissionAssignmentStatus.Accepted)
        {
            return ProviderMissionOperationResult.Ok(ToResponse(assignment));
        }

        try
        {
            assignment.Accept(request.Latitude, request.Longitude, request.AccuracyMeters);
            assignment.Mission.MarkProviderAccepted(assignment.ProviderId, assignment.CompanyId);
            return ProviderMissionOperationResult.Ok(ToResponse(assignment));
        }
        catch (InvalidOperationException exception)
        {
            return ProviderMissionOperationResult.BadRequest(exception.Message);
        }
    }

    public ProviderMissionOperationResult VerifyArrival(
        ProviderProfile provider,
        ProviderMissionAssignment assignment,
        ProviderLocationVerificationRequest request)
    {
        if (!CanProviderUsePortal(provider))
        {
            return ProviderMissionOperationResult.Forbidden("Ce prestataire n'est pas autorise a utiliser le portail.");
        }

        if (ProviderLocationPayloadValidator.Validate(request) is { } validationError)
        {
            return ProviderMissionOperationResult.BadRequest(validationError);
        }

        if (assignment.Mission is null)
        {
            return ProviderMissionOperationResult.NotFound("Mission introuvable pour ce prestataire.");
        }

        if (assignment.Status == ProviderMissionAssignmentStatus.Started && assignment.HasVerifiedArrival)
        {
            return ProviderMissionOperationResult.Ok(ToResponse(assignment));
        }

        if (assignment.Status != ProviderMissionAssignmentStatus.Accepted)
        {
            return ProviderMissionOperationResult.BadRequest("La presence ne peut etre verifiee que sur une mission acceptee.");
        }

        assignment.VerifyArrival(
            request.Latitude,
            request.Longitude,
            request.AccuracyMeters,
            assignment.Mission.ServiceLatitude,
            assignment.Mission.ServiceLongitude,
            assignment.Mission.ArrivalToleranceMeters);

        return ProviderMissionOperationResult.Ok(ToResponse(assignment));
    }

    public ProviderMissionOperationResult StartMission(
        ProviderProfile provider,
        ProviderMissionAssignment assignment,
        ProviderLocationVerificationRequest request)
    {
        if (!CanProviderUsePortal(provider))
        {
            return ProviderMissionOperationResult.Forbidden("Ce prestataire n'est pas autorise a utiliser le portail.");
        }

        if (ProviderLocationPayloadValidator.Validate(request) is { } validationError)
        {
            return ProviderMissionOperationResult.BadRequest(validationError);
        }

        if (assignment.Mission is null)
        {
            return ProviderMissionOperationResult.NotFound("Mission introuvable pour ce prestataire.");
        }

        if (assignment.Status == ProviderMissionAssignmentStatus.Started)
        {
            return ProviderMissionOperationResult.Ok(ToResponse(assignment));
        }

        if (assignment.Status != ProviderMissionAssignmentStatus.Accepted)
        {
            return ProviderMissionOperationResult.BadRequest("La mission doit etre acceptee avant de demarrer la prestation.");
        }

        if (!assignment.Mission.CanStartFor(assignment.ProviderId, assignment.CompanyId))
        {
            return ProviderMissionOperationResult.BadRequest("La mission n'est plus active ou ne correspond plus a cette affectation.");
        }

        assignment.VerifyArrival(
            request.Latitude,
            request.Longitude,
            request.AccuracyMeters,
            assignment.Mission.ServiceLatitude,
            assignment.Mission.ServiceLongitude,
            assignment.Mission.ArrivalToleranceMeters);

        if (!assignment.HasVerifiedArrival)
        {
            return ProviderMissionOperationResult.BadRequest(GetArrivalVerificationMessage(
                assignment.ArrivalVerificationStatus,
                assignment.ArrivalDistanceMeters,
                assignment.ArrivalToleranceMeters), ToResponse(assignment));
        }

        assignment.Start();
        assignment.Mission.Start(assignment.ProviderId, assignment.CompanyId);
        return ProviderMissionOperationResult.Ok(ToResponse(assignment));
    }

    public static bool CanProviderUsePortal(ProviderProfile provider)
    {
        return provider.Status == ProviderStatus.Approved
            && provider.Company?.Status == CompanyStatus.Approved;
    }

    public static ProviderLocationVerificationResponse ToResponse(ProviderMissionAssignment assignment)
    {
        return new ProviderLocationVerificationResponse(
            assignment.Id,
            assignment.MissionId,
            assignment.ArrivalVerificationStatus.ToString(),
            assignment.HasVerifiedArrival,
            assignment.ArrivalDistanceMeters,
            assignment.ArrivalToleranceMeters,
            assignment.ArrivalAccuracyMeters,
            GetArrivalVerificationMessage(assignment.ArrivalVerificationStatus, assignment.ArrivalDistanceMeters, assignment.ArrivalToleranceMeters),
            assignment.ArrivalVerifiedAt);
    }

    public static string GetArrivalVerificationMessage(LocationVerificationStatus status, int? distanceMeters, int toleranceMeters)
    {
        return status switch
        {
            LocationVerificationStatus.Verified => "Position verifiee. Le prestataire est dans la zone client.",
            LocationVerificationStatus.OutsideTolerance => $"Position trop eloignee du client. Distance estimee: {distanceMeters ?? 0} m, tolerance: {toleranceMeters} m.",
            LocationVerificationStatus.MissingProviderLocation => "Position du prestataire manquante. Activez la localisation puis reessayez.",
            LocationVerificationStatus.InvalidProviderLocation => "Position du prestataire invalide.",
            LocationVerificationStatus.MissingMissionLocation => "La mission ne contient pas encore de position client exploitable.",
            LocationVerificationStatus.LowAccuracy => "Precision GPS trop faible. Rapprochez-vous de l'exterieur ou reessayez.",
            _ => "Verification de presence non effectuee."
        };
    }
}
