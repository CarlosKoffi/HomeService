using HomeService.Application.Abstractions;
using HomeService.Application.CompanyPortal;
using HomeService.Application.Notifications;
using HomeService.Contracts.Clients;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Clients;

public sealed class ClientMissionCompletionValidationService(
    IAppDbContext db,
    CompanyPortalNotificationWriter companyNotifications,
    MobilePushNotificationQueueService mobilePushNotifications)
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
            request.PresentationRating,
            request.PolitenessRating,
            request.CleanlinessRating,
            request.Comment);
        db.MissionReviews.Add(review);
        AddCustomerCompletionPhotos(mission, request);

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

        companyNotifications.AddForMission(
            mission,
            "MissionPaymentReleased",
            $"Mission {mission.MissionNumber} validee",
            $"Le client a valide la mission avec une note globale de {review.OverallRating}/5. Reversement entreprise: {mission.CompanyPayoutAmount:N0} {mission.Currency}.",
            "success",
            $"missions/{mission.Id}");

        await mobilePushNotifications.QueueForOwnerAsync(
            MobileDeviceOwnerType.Provider,
            mission.ProviderId.Value,
            "Mission validee",
            $"Le client a valide la mission {mission.MissionNumber}. Note globale: {review.OverallRating}/5.",
            nameof(Mission),
            mission.Id,
            null,
            cancellationToken,
            saveChanges: false);

        await db.SaveChangesAsync(cancellationToken);
        return ClientMissionCompletionValidationResult.Ok(ToResponse(mission, review.OverallRating));
    }

    public async Task<int> AutoValidateExpiredAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var dueMissions = await db.Missions
            .Where(mission =>
                mission.Status == MissionStatus.Completed
                && mission.CustomerCompletionValidatedAt == null
                && mission.CustomerCompletionValidationExpiresAt != null
                && mission.CustomerCompletionValidationExpiresAt <= now
                && mission.CompanyId != null
                && mission.ProviderId != null)
            .OrderBy(mission => mission.CustomerCompletionValidationExpiresAt)
            .Take(Math.Clamp(batchSize, 1, 200))
            .ToListAsync(cancellationToken);

        var validatedCount = 0;
        foreach (var mission in dueMissions)
        {
            mission.ValidateCompletionByCustomer(now);

            var completionMilestone = await db.MissionPaymentMilestones
                .FirstOrDefaultAsync(item =>
                    item.MissionId == mission.Id
                    && item.Trigger == MissionPaymentMilestoneTrigger.MissionCompleted,
                    cancellationToken);
            if (completionMilestone is not null && completionMilestone.Status != MissionPaymentMilestoneStatus.Paid)
            {
                completionMilestone.MarkPaid($"AUTO-COMPLETION-{mission.Id:N}");
            }

            db.CompanyPortalActivities.Add(new CompanyPortalActivity(
                mission.CompanyId!.Value,
                "mission",
                "Mission validee automatiquement",
                $"Le delai de validation client de la mission {mission.MissionNumber} a expire. Le paiement entreprise a ete libere automatiquement.",
                "green",
                nameof(Mission),
                mission.Id));

            companyNotifications.AddForMission(
                mission,
                "MissionCompletionAutoValidated",
                $"Mission {mission.MissionNumber} validee automatiquement",
                $"Le delai client a expire. Le reversement de {mission.CompanyPayoutAmount:N0} {mission.Currency} a ete libere automatiquement.",
                "success",
                $"missions/{mission.Id}");

            await mobilePushNotifications.QueueForOwnerAsync(
                MobileDeviceOwnerType.Provider,
                mission.ProviderId!.Value,
                "Mission validee automatiquement",
                $"Le delai client de la mission {mission.MissionNumber} a expire. La mission est maintenant validee.",
                nameof(Mission),
                mission.Id,
                null,
                cancellationToken,
                saveChanges: false);

            validatedCount++;
        }

        if (validatedCount > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return validatedCount;
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
        ValidateRating(request.PresentationRating, "presentation", errors);
        ValidateRating(request.PolitenessRating, "politesse", errors);
        ValidateRating(request.CleanlinessRating, "proprete", errors);
        ValidatePhotos(request.Photos, errors);
        return errors;
    }

    private void AddCustomerCompletionPhotos(Mission mission, ValidateClientMissionCompletionRequest request)
    {
        if (request.Photos is null)
        {
            return;
        }

        foreach (var photo in request.Photos.Take(MaxCompletionPhotos))
        {
            db.MissionAttachments.Add(new MissionAttachment(
                mission.Id,
                MissionAttachmentType.CustomerCompletionPhoto,
                photo.OriginalFileName,
                photo.StoragePath,
                photo.ContentType,
                photo.FileSizeBytes,
                photo.Caption));
        }
    }

    private static void ValidatePhotos(IReadOnlyList<ClientMissionPhotoRequest>? photos, List<string> errors)
    {
        if (photos is null || photos.Count == 0)
        {
            return;
        }

        if (photos.Count > MaxCompletionPhotos)
        {
            errors.Add($"Ajoutez {MaxCompletionPhotos} photos maximum pour votre avis.");
        }

        foreach (var photo in photos)
        {
            if (string.IsNullOrWhiteSpace(photo.OriginalFileName) || photo.OriginalFileName.Length > 260)
            {
                errors.Add("Chaque photo doit avoir un nom de fichier valide.");
            }

            var normalizedPath = photo.StoragePath?.Replace('\\', '/').Trim();
            if (string.IsNullOrWhiteSpace(normalizedPath)
                || normalizedPath.Length > 720
                || !normalizedPath.StartsWith("client-missions/pending/", StringComparison.OrdinalIgnoreCase)
                || normalizedPath.Contains("../", StringComparison.Ordinal)
                || normalizedPath.Contains("/..", StringComparison.Ordinal))
            {
                errors.Add("Le chemin de stockage d'une photo est invalide.");
            }

            if (string.IsNullOrWhiteSpace(photo.ContentType)
                || photo.ContentType.Length > 120
                || !photo.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Les pieces jointes de l'avis doivent etre des images.");
            }

            if (photo.FileSizeBytes is <= 0 or > MaxCompletionPhotoBytes)
            {
                errors.Add("Chaque photo de l'avis doit faire moins de 5 Mo.");
            }

            if (photo.Caption?.Length > 500)
            {
                errors.Add("La legende d'une photo ne peut pas depasser 500 caracteres.");
            }
        }
    }

    private static void ValidateRating(int rating, string label, List<string> errors)
    {
        if (rating is < 1 or > 5)
        {
            errors.Add($"La note {label} doit etre comprise entre 1 et 5.");
        }
    }

    private const int MaxCompletionPhotos = 4;
    private const long MaxCompletionPhotoBytes = 5 * 1024 * 1024;
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
