using System.Text.Json;
using HomeService.Application.Abstractions;
using HomeService.Application.CompanyPortal;
using HomeService.Application.Missions;
using HomeService.Application.Notifications;
using HomeService.Contracts.Clients;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Clients;

public sealed class ClientMissionConfirmationService(
    IAppDbContext db,
    CompanyPortalNotificationWriter companyNotifications,
    MobilePushNotificationQueueService mobilePushNotifications,
    MissionCommercialPricingService? commercialPricing = null,
    IClientPaymentGateway? clientPaymentGateway = null)
{
    public async Task<ClientMissionConfirmationResult> ConfirmAsync(
        Guid missionId,
        ConfirmClientMissionRequest request,
        CancellationToken cancellationToken)
    {
        var validationErrors = Validate(request);
        if (validationErrors.Count > 0)
        {
            return ClientMissionConfirmationResult.ValidationFailed(validationErrors);
        }

        var mission = await db.Missions
            .FirstOrDefaultAsync(item => item.Id == missionId, cancellationToken);
        if (mission is null)
        {
            return ClientMissionConfirmationResult.NotFound("Mission introuvable.");
        }

        var customer = await db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == mission.CustomerId, cancellationToken);
        if (customer is null)
        {
            return ClientMissionConfirmationResult.Invalid("Client introuvable pour cette mission.");
        }

        if (!string.Equals(customer.PhoneNumber, request.PhoneNumber.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return ClientMissionConfirmationResult.Forbidden("Ce numero ne correspond pas au client de la mission.");
        }

        if (mission.CustomerConfirmedAt is null && mission.Status != MissionStatus.Accepted)
        {
            return ClientMissionConfirmationResult.Invalid(
                "Le paiement sera disponible lorsque le prestataire aura accepte la mission.");
        }

        if (mission.CompanyId is null || mission.ProviderId is null)
        {
            return ClientMissionConfirmationResult.Invalid("La mission doit avoir une entreprise et un prestataire affectes.");
        }

        if (!mission.CustomerPaymentMethodId.HasValue)
        {
            return ClientMissionConfirmationResult.Invalid("Choisissez un moyen de paiement avant de confirmer le prix.");
        }

        var company = await db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == mission.CompanyId.Value, cancellationToken);
        var provider = await db.Providers
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == mission.ProviderId.Value, cancellationToken);
        if (company is null || provider is null)
        {
            return ClientMissionConfirmationResult.Invalid("L'entreprise ou le prestataire affecte est introuvable.");
        }

        var totalAmount = mission.CompanyQuotedAmount ?? mission.EstimatedTotalAmount ?? mission.FinalTotalAmount;
        if (totalAmount is null or <= 0)
        {
            return ClientMissionConfirmationResult.Invalid("Aucun montant valide n'est disponible pour cette mission.");
        }

        if (mission.CustomerConfirmedAt is null)
        {
            await ConfirmMissionAsync(
                mission,
                company,
                totalAmount.Value,
                request.PaymentReference,
                automatic: false,
                cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        return ClientMissionConfirmationResult.Ok(ToResponse(mission, company, provider));
    }

    public async Task<ClientMissionConfirmationResult> ConfirmVerifiedPaymentAsync(
        Guid missionId,
        string paymentReference,
        CancellationToken cancellationToken)
    {
        var mission = await db.Missions
            .FirstOrDefaultAsync(item => item.Id == missionId, cancellationToken);
        if (mission is null)
        {
            return ClientMissionConfirmationResult.NotFound("Mission introuvable.");
        }

        if (mission.CustomerConfirmedAt is not null)
        {
            var existingCompany = mission.CompanyId.HasValue
                ? await db.Companies.AsNoTracking().FirstOrDefaultAsync(item => item.Id == mission.CompanyId.Value, cancellationToken)
                : null;
            var existingProvider = mission.ProviderId.HasValue
                ? await db.Providers.AsNoTracking().FirstOrDefaultAsync(item => item.Id == mission.ProviderId.Value, cancellationToken)
                : null;
            return existingCompany is not null && existingProvider is not null
                ? ClientMissionConfirmationResult.Ok(ToResponse(mission, existingCompany, existingProvider))
                : ClientMissionConfirmationResult.Invalid("L'affectation de la mission est incomplete.");
        }

        if (mission.Status != MissionStatus.Accepted || mission.CompanyId is null || mission.ProviderId is null)
        {
            return ClientMissionConfirmationResult.Invalid(
                "La mission n'est plus dans un etat permettant de confirmer le paiement.");
        }

        var company = await db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == mission.CompanyId.Value, cancellationToken);
        var provider = await db.Providers
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == mission.ProviderId.Value, cancellationToken);
        var totalAmount = mission.CompanyQuotedAmount ?? mission.EstimatedTotalAmount ?? mission.FinalTotalAmount;
        if (company is null || provider is null || totalAmount is null or <= 0)
        {
            return ClientMissionConfirmationResult.Invalid("Les donnees de paiement de la mission sont incompletes.");
        }

        await ConfirmMissionAsync(
            mission,
            company,
            totalAmount.Value,
            paymentReference,
            automatic: false,
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ClientMissionConfirmationResult.Ok(ToResponse(mission, company, provider));
    }

    public async Task<int> AutoConfirmExpiredAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken)
    {
        // Jeko ne fournit ni debit automatique mandate ni verification du solde du payeur.
        // En production, seule une transaction Jeko signee "success" peut confirmer la mission.
        if (clientPaymentGateway?.IsEnabled == true)
        {
            return 0;
        }

        var dueMissions = await db.Missions
            .Where(mission =>
                mission.Status == MissionStatus.Accepted
                && mission.CustomerConfirmedAt == null
                && mission.CustomerPaymentExpiresAt != null
                && mission.CustomerPaymentExpiresAt <= now
                && mission.CustomerPaymentMethodId != null
                && mission.CompanyId != null
                && mission.ProviderId != null
                && (mission.CompanyQuotedAmount > 0 || mission.EstimatedTotalAmount > 0 || mission.FinalTotalAmount > 0))
            .OrderBy(mission => mission.CustomerPaymentExpiresAt)
            .Take(Math.Clamp(batchSize, 1, 200))
            .ToListAsync(cancellationToken);

        var confirmedCount = 0;
        foreach (var mission in dueMissions)
        {
            var company = await db.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == mission.CompanyId, cancellationToken);
            if (company is null)
            {
                continue;
            }

            var totalAmount = mission.CompanyQuotedAmount ?? mission.EstimatedTotalAmount ?? mission.FinalTotalAmount;
            if (totalAmount is null or <= 0)
            {
                continue;
            }

            await ConfirmMissionAsync(
                mission,
                company,
                totalAmount.Value,
                $"AUTO-CLIENT-DEADLINE-{mission.Id:N}",
                automatic: true,
                cancellationToken);
            confirmedCount++;
        }

        if (confirmedCount > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return confirmedCount;
    }

    private async Task ConfirmMissionAsync(
        Mission mission,
        Company company,
        int totalAmount,
        string? paymentReference,
        bool automatic,
        CancellationToken cancellationToken)
    {
        var pricing = await (commercialPricing ?? new MissionCommercialPricingService(db))
            .CalculateAsync(mission, totalAmount, cancellationToken);
        mission.AcceptCompanyQuote();
        mission.ConfirmByCustomer(
            pricing.CompanyCommissionAmount,
            mission.TransportFeeAmount,
            pricing.CompanyCommissionRateBasisPoints,
            0,
            pricing.CustomerServiceFeeAmount,
            pricing.CustomerServiceFeeRateBasisPoints,
            pricing.CustomerTotalAmount,
            pricing.CommissionableAmount,
            pricing.IsFirstCustomerCompanyOrder,
            pricing.CompanyCommissionTierName,
            pricing.CompanyCommissionMissionSequence);

        var milestone = new MissionPaymentMilestone(
            mission.Id,
            MissionPaymentMilestoneTrigger.QuoteAccepted,
            pricing.CustomerTotalAmount,
            pricing.Currency,
            automatic
                ? "Paiement client confirme automatiquement a expiration du delai"
                : "Paiement client bloque a l'acceptation du devis",
            0);
        milestone.MarkDue(DateTimeOffset.UtcNow);
        milestone.MarkPaid(paymentReference);
        db.MissionPaymentMilestones.Add(milestone);

        var activityTitle = automatic ? "Devis valide automatiquement" : "Devis accepte par le client";
        var activityMessage = automatic
            ? $"Le delai client de la mission {mission.MissionNumber} a expire. Le moyen de paiement selectionne a ete utilise et les contacts sont visibles."
            : $"Le client a accepte la mission {mission.MissionNumber}. Les contacts sont maintenant visibles.";
        db.CompanyPortalActivities.Add(new CompanyPortalActivity(
            company.Id,
            "mission",
            activityTitle,
            activityMessage,
            "green",
            nameof(Mission),
            mission.Id));

        companyNotifications.AddForMission(
            mission,
            automatic ? "MissionQuoteAutoAccepted" : "MissionQuoteAcceptedByCustomer",
            automatic
                ? $"Devis valide automatiquement pour {mission.MissionNumber}"
                : $"Devis accepte pour {mission.MissionNumber}",
            automatic
                ? $"Le delai client a expire. Le devis de {totalAmount:N0} {mission.Currency} a ete confirme automatiquement avec le moyen de paiement selectionne."
                : $"Le client a accepte le devis de {totalAmount:N0} {mission.Currency}. Les contacts sont maintenant visibles.",
            "success",
            $"missions/{mission.Id}");

        await mobilePushNotifications.QueueForOwnerAsync(
            MobileDeviceOwnerType.Company,
            company.Id,
            automatic ? "Paiement confirmé automatiquement" : "Paiement client confirmé",
            automatic
                ? $"Le délai client de la mission {mission.MissionNumber} a expiré. Le paiement a été confirmé automatiquement."
                : $"Le client a payé la mission {mission.MissionNumber}. L’intervention peut être préparée.",
            nameof(Mission),
            mission.Id,
            JsonSerializer.Serialize(new
            {
                type = automatic ? "company_customer_payment_auto_confirmed" : "company_customer_payment_confirmed",
                missionId = mission.Id,
                missionNumber = mission.MissionNumber,
                providerId = mission.ProviderId,
                companyId = company.Id
            }),
            cancellationToken,
            saveChanges: false);

        await mobilePushNotifications.QueueForOwnerAsync(
            MobileDeviceOwnerType.Provider,
            mission.ProviderId!.Value,
            "Mission confirmee",
            automatic
                ? $"La mission {mission.MissionNumber} a ete confirmee automatiquement. Preparez votre intervention."
                : $"Le client a confirme la mission {mission.MissionNumber}. Preparez votre intervention.",
            nameof(Mission),
            mission.Id,
            JsonSerializer.Serialize(new
            {
                type = "provider_mission_payment_confirmed",
                missionId = mission.Id,
                missionNumber = mission.MissionNumber,
                assignmentId = await db.ProviderMissionAssignments
                    .Where(item => item.MissionId == mission.Id && item.ProviderId == mission.ProviderId)
                    .OrderByDescending(item => item.CreatedAt)
                    .Select(item => (Guid?)item.Id)
                    .FirstOrDefaultAsync(cancellationToken),
                providerId = mission.ProviderId,
                companyId = mission.CompanyId
            }),
            cancellationToken,
            saveChanges: false);
    }

    private static ConfirmClientMissionResponse ToResponse(Mission mission, Company company, ProviderProfile provider)
    {
        return new ConfirmClientMissionResponse(
            mission.Id,
            mission.MissionNumber,
            mission.Status.ToString(),
            mission.PaymentStatus.ToString(),
            mission.CompanyQuotedAmount ?? mission.EstimatedTotalAmount ?? mission.FinalTotalAmount ?? 0,
            mission.CustomerServiceFeeAmount,
            mission.CustomerServiceFeeRateBasisPoints,
            mission.CustomerChargedAmount,
            mission.Currency,
            mission.CanRevealContactDetails,
            mission.ContactDetailsReleasedAt,
            company.Name,
            company.PhoneNumber,
            provider.FullName,
            provider.PhoneNumber);
    }

    private static List<string> Validate(ConfirmClientMissionRequest request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            errors.Add("Le numero de telephone client est obligatoire.");
        }

        return errors;
    }
}

public sealed record ClientMissionConfirmationResult(
    ClientMissionConfirmationStatus Status,
    IReadOnlyList<string> Errors,
    ConfirmClientMissionResponse? Response,
    string? Message)
{
    public bool IsSuccess => Status == ClientMissionConfirmationStatus.Ok;

    public static ClientMissionConfirmationResult Ok(ConfirmClientMissionResponse response)
    {
        return new ClientMissionConfirmationResult(ClientMissionConfirmationStatus.Ok, [], response, null);
    }

    public static ClientMissionConfirmationResult ValidationFailed(IReadOnlyList<string> errors)
    {
        return new ClientMissionConfirmationResult(ClientMissionConfirmationStatus.ValidationFailed, errors, null, "Confirmation invalide.");
    }

    public static ClientMissionConfirmationResult Invalid(string message)
    {
        return new ClientMissionConfirmationResult(ClientMissionConfirmationStatus.Invalid, [], null, message);
    }

    public static ClientMissionConfirmationResult Forbidden(string message)
    {
        return new ClientMissionConfirmationResult(ClientMissionConfirmationStatus.Forbidden, [], null, message);
    }

    public static ClientMissionConfirmationResult NotFound(string message)
    {
        return new ClientMissionConfirmationResult(ClientMissionConfirmationStatus.NotFound, [], null, message);
    }
}

public enum ClientMissionConfirmationStatus
{
    Ok,
    ValidationFailed,
    Invalid,
    Forbidden,
    NotFound
}
