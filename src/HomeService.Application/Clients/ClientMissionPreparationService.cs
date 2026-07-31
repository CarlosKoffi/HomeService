using HomeService.Application.Abstractions;
using HomeService.Application.Missions;
using HomeService.Contracts.Clients;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Clients;

public sealed class ClientMissionPreparationService(IAppDbContext db)
{
    public async Task<ClientMissionPreparationResult> PrepareAsync(
        PrepareClientMissionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ServiceId == Guid.Empty)
        {
            return ClientMissionPreparationResult.NotFound("Choisissez un service avant de continuer.");
        }

        var service = await db.Services
            .AsNoTracking()
            .Include(item => item.Prestations)
            .FirstOrDefaultAsync(item => item.Id == request.ServiceId && item.IsActive, cancellationToken);

        if (service is null)
        {
            return ClientMissionPreparationResult.NotFound("Service introuvable ou inactif.");
        }

        var prestation = request.ServicePrestationId.HasValue
            ? service.Prestations.FirstOrDefault(item => item.Id == request.ServicePrestationId.Value && item.IsActive)
            : null;

        if (request.ServicePrestationId.HasValue && prestation is null)
        {
            return ClientMissionPreparationResult.Invalid("La prestation choisie ne correspond pas au service.");
        }

        var urgentOptionEnabled = await MissionWorkflowSettingsResolver.ResolveFlagAsync(
            db,
            MissionWorkflowSettingsResolver.UrgentMissionsEnabled,
            fallbackValue: false,
            cancellationToken);
        var isInstant = string.Equals(request.Mode, "Instant", StringComparison.OrdinalIgnoreCase);
        var isUrgent = urgentOptionEnabled && isInstant && request.IsUrgent;
        var companyResponseWindow = await MissionWorkflowSettingsResolver.ResolveMinutesAsync(
            db,
            isUrgent
                ? MissionWorkflowSettingsResolver.UrgentCompanyOfferResponseMinutes
                : MissionWorkflowSettingsResolver.CompanyOfferResponseMinutes,
            isUrgent ? 5 : 15,
            cancellationToken);
        var assignmentWindow = await MissionWorkflowSettingsResolver.ResolveMinutesAsync(
            db,
            MissionWorkflowSettingsResolver.CompanyProviderAssignmentMinutes,
            10,
            cancellationToken);

        var priceMinAmount = prestation?.PriceMinAmount ?? service.PriceMinAmount;
        var priceMaxAmount = prestation?.PriceMaxAmount ?? service.PriceMaxAmount;
        var currency = prestation?.Currency ?? service.Currency;
        var photosRecommended = service.RequiresCompletionPhoto || service.RequiresBeforeAfterPhotos || service.RequiresAdminApprovalBeforeAssignment;
        var displayName = prestation is null ? service.Name : $"{service.Name} - {prestation.Name}";

        return ClientMissionPreparationResult.Ok(new PrepareClientMissionResponse(
            service.Id,
            service.Name,
            prestation?.Id,
            prestation?.Name,
            displayName,
            prestation?.Description ?? service.Description,
            service.IconName,
            service.IconUrl,
            prestation?.IllustrationUrl ?? service.ImageUrl,
            priceMinAmount,
            priceMaxAmount,
            currency,
            RequiresCompanyQuote: true,
            PhotosRecommended: photosRecommended,
            PhotosRequired: false,
            MaxPhotoCount: 5,
            EstimatedDurationMinutes: 90,
            Mode: string.IsNullOrWhiteSpace(request.Mode) ? "Instant" : request.Mode.Trim(),
            IsUrgent: isUrgent,
            UrgentOptionEnabled: urgentOptionEnabled,
            CompanyResponseMinutes: (int)Math.Ceiling(companyResponseWindow.TotalMinutes),
            CompanyAssignmentMinutes: (int)Math.Ceiling(assignmentWindow.TotalMinutes),
            PaymentOptions:
            [
                new ClientMissionPaymentOptionResponse("MobileMoney", "Mobile Money", IsAvailable: true, IsRecommended: true),
                new ClientMissionPaymentOptionResponse("Card", "Carte bancaire", IsAvailable: true, IsRecommended: false)
            ],
            RecommendedPaymentMethod: "MobileMoney",
            Message: BuildMessage(displayName, priceMinAmount, priceMaxAmount, currency)));
    }

    private static string BuildMessage(string displayName, int priceMinAmount, int priceMaxAmount, string currency)
    {
        return $"Votre demande {displayName} sera proposee aux entreprises disponibles. Le client voit un prix a partir de {priceMinAmount:N0} {currency}; l'entreprise confirmera le prix final dans la fourchette jusqu'a {priceMaxAmount:N0} {currency}.";
    }
}

public sealed record ClientMissionPreparationResult(
    bool IsSuccess,
    bool IsNotFound,
    string? Message,
    PrepareClientMissionResponse? Response)
{
    public static ClientMissionPreparationResult Ok(PrepareClientMissionResponse response)
    {
        return new ClientMissionPreparationResult(true, false, null, response);
    }

    public static ClientMissionPreparationResult Invalid(string message)
    {
        return new ClientMissionPreparationResult(false, false, message, null);
    }

    public static ClientMissionPreparationResult NotFound(string message)
    {
        return new ClientMissionPreparationResult(false, true, message, null);
    }
}
