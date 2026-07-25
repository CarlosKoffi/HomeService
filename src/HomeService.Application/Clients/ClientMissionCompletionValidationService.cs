using HomeService.Application.Abstractions;
using HomeService.Contracts.Clients;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Clients;

public sealed class ClientMissionCompletionValidationService(IAppDbContext db)
{
    public async Task<ClientMissionCompletionValidationResult> ValidateAsync(
        Guid missionId,
        ValidateClientMissionCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var validationErrors = Validate(request);
        if (validationErrors.Count > 0)
        {
            return ClientMissionCompletionValidationResult.ValidationFailed(validationErrors);
        }

        var mission = await db.Missions.FirstOrDefaultAsync(item => item.Id == missionId, cancellationToken);
        if (mission is null)
        {
            return ClientMissionCompletionValidationResult.NotFound("Mission introuvable.");
        }

        var customer = await db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == mission.CustomerId, cancellationToken);
        if (customer is null)
        {
            return ClientMissionCompletionValidationResult.Invalid("Client introuvable pour cette mission.");
        }

        if (!string.Equals(customer.PhoneNumber, request.PhoneNumber.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return ClientMissionCompletionValidationResult.Forbidden("Ce numero ne correspond pas au client de la mission.");
        }

        if (mission.CompanyId is null || mission.ProviderId is null)
        {
            return ClientMissionCompletionValidationResult.Invalid("La mission doit avoir une entreprise et un prestataire affectes.");
        }

        if (mission.Status != MissionStatus.Completed)
        {
            return ClientMissionCompletionValidationResult.Invalid("La mission doit etre terminee avant validation client.");
        }

        var existingReview = await db.MissionReviews
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.MissionId == mission.Id, cancellationToken);
        if (existingReview is not null)
        {
            return ClientMissionCompletionValidationResult.Ok(ToResponse(mission, existingReview.OverallRating));
        }

        var review = new MissionReview(
            mission.Id,
            mission.CustomerId,
            mission.CompanyId.Value,
            mission.ProviderId.Value,
            request.QualityRating,
            request.PunctualityRating,
            request.PolitenessRating,
            request.CleanlinessRating,
            request.Comment);
        db.MissionReviews.Add(review);

        mission.ValidateCompletionByCustomer();

        var completionMilestone = await db.MissionPaymentMilestones
            .FirstOrDefaultAsync(item =>
                item.MissionId == mission.Id
                && item.Trigger == MissionPaymentMilestoneTrigger.MissionCompleted,
                cancellationToken);
        if (completionMilestone is not null && completionMilestone.Status != MissionPaymentMilestoneStatus.Paid)
        {
            completionMilestone.MarkPaid(request.PayoutReference);
        }

        db.CompanyPortalActivities.Add(new CompanyPortalActivity(
            mission.CompanyId.Value,
            "mission",
            "Mission validee par le client",
            $"Le client a valide la mission {mission.MissionNumber} avec une note globale de {review.OverallRating}/5.",
            "green",
            nameof(Mission),
            mission.Id));

        await db.SaveChangesAsync(cancellationToken);
        return ClientMissionCompletionValidationResult.Ok(ToResponse(mission, review.OverallRating));
    }

    private static ValidateClientMissionCompletionResponse ToResponse(Mission mission, int overallRating)
    {
        return new ValidateClientMissionCompletionResponse(
            mission.Id,
            mission.MissionNumber,
            mission.Status.ToString(),
            mission.PaymentStatus.ToString(),
            mission.CustomerCompletionValidatedAt,
            mission.CompanyPayoutReleasedAt,
            overallRating,
            mission.CompanyPayoutAmount,
            mission.Currency);
    }

    private static List<string> Validate(ValidateClientMissionCompletionRequest request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            errors.Add("Le numero de telephone client est obligatoire.");
        }

        ValidateRating(request.QualityRating, "qualite", errors);
        ValidateRating(request.PunctualityRating, "ponctualite", errors);
        ValidateRating(request.PolitenessRating, "politesse", errors);
        ValidateRating(request.CleanlinessRating, "proprete", errors);
        return errors;
    }

    private static void ValidateRating(int rating, string label, List<string> errors)
    {
        if (rating is < 1 or > 5)
        {
            errors.Add($"La note {label} doit etre comprise entre 1 et 5.");
        }
    }
}

public sealed record ClientMissionCompletionValidationResult(
    ClientMissionCompletionValidationStatus Status,
    IReadOnlyList<string> Errors,
    ValidateClientMissionCompletionResponse? Response,
    string? Message)
{
    public bool IsSuccess => Status == ClientMissionCompletionValidationStatus.Ok;

    public static ClientMissionCompletionValidationResult Ok(ValidateClientMissionCompletionResponse response)
        => new(ClientMissionCompletionValidationStatus.Ok, [], response, null);

    public static ClientMissionCompletionValidationResult ValidationFailed(IReadOnlyList<string> errors)
        => new(ClientMissionCompletionValidationStatus.ValidationFailed, errors, null, "Validation mission invalide.");

    public static ClientMissionCompletionValidationResult Invalid(string message)
        => new(ClientMissionCompletionValidationStatus.Invalid, [], null, message);

    public static ClientMissionCompletionValidationResult Forbidden(string message)
        => new(ClientMissionCompletionValidationStatus.Forbidden, [], null, message);

    public static ClientMissionCompletionValidationResult NotFound(string message)
        => new(ClientMissionCompletionValidationStatus.NotFound, [], null, message);
}

public enum ClientMissionCompletionValidationStatus
{
    Ok,
    ValidationFailed,
    Invalid,
    Forbidden,
    NotFound
}
