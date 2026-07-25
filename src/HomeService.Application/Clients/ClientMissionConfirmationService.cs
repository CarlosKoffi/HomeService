using HomeService.Application.Abstractions;
using HomeService.Contracts.Clients;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Clients;

public sealed class ClientMissionConfirmationService(IAppDbContext db)
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

        if (mission.CompanyId is null || mission.ProviderId is null)
        {
            return ClientMissionConfirmationResult.Invalid("La mission doit avoir une entreprise et un prestataire affectes.");
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
            if (mission.Status != MissionStatus.Accepted)
            {
                return ClientMissionConfirmationResult.Invalid("Le prestataire doit accepter la mission avant la confirmation client.");
            }

            var commissionRule = await ResolvePlatformCommissionRuleAsync(mission, cancellationToken);
            var platformCommissionAmount = commissionRule.CalculateAmount(totalAmount.Value);
            mission.AcceptCompanyQuote();
            mission.ConfirmByCustomer(
                platformCommissionAmount,
                mission.TransportFeeAmount,
                commissionRule.RateBasisPoints,
                0);

            var milestone = new MissionPaymentMilestone(
                mission.Id,
                MissionPaymentMilestoneTrigger.QuoteAccepted,
                totalAmount.Value,
                commissionRule.Currency,
                "Paiement client bloque a l'acceptation du devis",
                0);
            milestone.MarkDue(DateTimeOffset.UtcNow);
            milestone.MarkPaid(request.PaymentReference);
            db.MissionPaymentMilestones.Add(milestone);

            db.CompanyPortalActivities.Add(new CompanyPortalActivity(
                company.Id,
                "mission",
                "Devis accepte par le client",
                $"Le client a accepte la mission {mission.MissionNumber}. Les contacts sont maintenant visibles.",
                "green",
                nameof(Mission),
                mission.Id));

            await db.SaveChangesAsync(cancellationToken);
        }

        return ClientMissionConfirmationResult.Ok(ToResponse(mission, company, provider, totalAmount.Value));
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
