using HomeService.Application.Abstractions;
using HomeService.Application.CompanyPortal;
using HomeService.Application.Notifications;
using HomeService.Contracts.Clients;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Clients;

public sealed class ClientMissionConfirmationService(
    IAppDbContext db,
    CompanyPortalNotificationWriter companyNotifications,
    MobilePushNotificationQueueService mobilePushNotifications)
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

        return ClientMissionConfirmationResult.Ok(ToResponse(mission, company, provider, totalAmount.Value));
    }

    public async Task<int> AutoConfirmExpiredAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken)
    {
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
        var commissionRule = await ResolvePlatformCommissionRuleAsync(mission, cancellationToken);
        var platformCommissionAmount = commissionRule.CalculateAmount(totalAmount);
        mission.AcceptCompanyQuote();
        mission.ConfirmByCustomer(
            platformCommissionAmount,
            mission.TransportFeeAmount,
            commissionRule.RateBasisPoints,
            0);

        var milestone = new MissionPaymentMilestone(
            mission.Id,
            MissionPaymentMilestoneTrigger.QuoteAccepted,
            totalAmount,
            commissionRule.Currency,
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
            MobileDeviceOwnerType.Provider,
            mission.ProviderId!.Value,
            "Mission confirmee",
            automatic
                ? $"La mission {mission.MissionNumber} a ete confirmee automatiquement. Preparez votre intervention."
                : $"Le client a confirme la mission {mission.MissionNumber}. Preparez votre intervention.",
            nameof(Mission),
            mission.Id,
            null,
            cancellationToken,
            saveChanges: false);
    }

    private async Task<CommissionRule> ResolvePlatformCommissionRuleAsync(Mission mission, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var rules = await db.CommissionRules
            .AsNoTracking()
            .Where(rule => rule.IsActive
                && rule.Target == CommissionRuleTarget.PlatformConnection
                && rule.EffectiveFrom <= now
                && (rule.EffectiveUntil == null || rule.EffectiveUntil > now)
                && (rule.CompanyId == null || rule.CompanyId == mission.CompanyId)
                && (rule.ServiceId == null || rule.ServiceId == mission.ServiceId)
                && (rule.ServicePrestationId == null || rule.ServicePrestationId == mission.ServicePrestationId)
                && (rule.AssignmentSource == null || rule.AssignmentSource == mission.AssignmentSource))
            .ToListAsync(cancellationToken);

        return rules
            .OrderByDescending(rule => rule.CompanyId.HasValue)
            .ThenByDescending(rule => rule.ServicePrestationId.HasValue)
            .ThenByDescending(rule => rule.ServiceId.HasValue)
            .ThenByDescending(rule => rule.AssignmentSource.HasValue)
            .ThenByDescending(rule => rule.EffectiveFrom)
            .FirstOrDefault()
            ?? new CommissionRule("Commission mise en relation wélé", CommissionRuleTarget.PlatformConnection, 1500, 0, "XOF");
    }

    private static ConfirmClientMissionResponse ToResponse(Mission mission, Company company, ProviderProfile provider, int totalAmount)
    {
        return new ConfirmClientMissionResponse(
            mission.Id,
            mission.MissionNumber,
            mission.Status.ToString(),
            mission.PaymentStatus.ToString(),
            totalAmount,
            mission.PlatformCommissionAmount,
            mission.CompanyPayoutAmount,
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
