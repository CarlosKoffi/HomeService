using HomeService.Application.Abstractions;
using HomeService.Application.CompanyPortal;
using HomeService.Application.Notifications;
using HomeService.Contracts.Missions;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Missions;

public sealed class MissionAdditionalQuoteWorkflowService(
    IAppDbContext db,
    CompanyPortalNotificationWriter companyNotifications,
    MobilePushNotificationQueueService mobilePushNotifications)
{
    public async Task<MissionAdditionalQuoteWorkflowResult> RequestFromProviderAsync(
        Guid providerId,
        Guid missionId,
        RequestMissionAdditionalQuoteRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return MissionAdditionalQuoteWorkflowResult.ValidationFailed("Expliquez le besoin de devis complementaire.");
        }

        var mission = await db.Missions
            .FirstOrDefaultAsync(item => item.Id == missionId && item.ProviderId == providerId, cancellationToken);
        if (mission is null)
        {
            return MissionAdditionalQuoteWorkflowResult.NotFound("Mission introuvable pour ce prestataire.");
        }

        if (mission.CompanyId is null)
        {
            return MissionAdditionalQuoteWorkflowResult.ValidationFailed("La mission n'est pas rattachee a une entreprise.");
        }

        if (mission.Status != MissionStatus.Started)
        {
            return MissionAdditionalQuoteWorkflowResult.ValidationFailed("Le devis complementaire peut etre demande apres le debut de mission.");
        }

        var provider = await db.Providers
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == providerId, cancellationToken);
        if (provider is null)
        {
            return MissionAdditionalQuoteWorkflowResult.NotFound("Prestataire introuvable.");
        }

        var quote = new MissionAdditionalQuote(
            mission.Id,
            providerId,
            mission.CompanyId.Value,
            request.Reason,
            request.PhotoStoragePath);
        db.MissionAdditionalQuotes.Add(quote);

        companyNotifications.AddForMission(
            mission,
            "MissionAdditionalQuoteRequested",
            $"Devis complementaire demande pour {mission.MissionNumber}",
            $"{provider.FullName} demande un devis complementaire. {request.Reason.Trim()}",
            "warning",
            $"missions/{mission.Id}");

        db.CompanyPortalActivities.Add(new CompanyPortalActivity(
            mission.CompanyId.Value,
            "mission",
            "Devis complementaire demande",
            $"{provider.FullName} signale un besoin complementaire sur {mission.MissionNumber}.",
            "orange",
            nameof(MissionAdditionalQuote),
            quote.Id));

        await db.SaveChangesAsync(cancellationToken);
        return MissionAdditionalQuoteWorkflowResult.Ok(ToResponse(quote, mission));
    }

    public async Task<MissionAdditionalQuoteWorkflowResult> SubmitByCompanyAsync(
        Guid companyId,
        Guid quoteId,
        SubmitMissionAdditionalQuoteRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            return MissionAdditionalQuoteWorkflowResult.ValidationFailed("Le montant doit etre positif.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return MissionAdditionalQuoteWorkflowResult.ValidationFailed("Le detail du devis est obligatoire.");
        }

        var quote = await db.MissionAdditionalQuotes
            .Include(item => item.Mission)
            .FirstOrDefaultAsync(item => item.Id == quoteId && item.CompanyId == companyId, cancellationToken);
        if (quote?.Mission is null)
        {
            return MissionAdditionalQuoteWorkflowResult.NotFound("Devis complementaire introuvable.");
        }

        try
        {
            quote.Submit(request.Amount, request.Currency, request.Description);
        }
        catch (InvalidOperationException exception)
        {
            return MissionAdditionalQuoteWorkflowResult.ValidationFailed(exception.Message);
        }

        companyNotifications.AddForMission(
            quote.Mission,
            "MissionAdditionalQuoteSent",
            $"Devis complementaire envoye pour {quote.Mission.MissionNumber}",
            $"Le devis complementaire de {request.Amount:N0} {quote.Currency} a ete transmis au client.",
            "info",
            $"missions/{quote.Mission.Id}");

        await mobilePushNotifications.QueueForOwnerAsync(
            MobileDeviceOwnerType.Customer,
            quote.Mission.CustomerId,
            "Devis complementaire disponible",
            $"Un devis complementaire de {request.Amount:N0} {quote.Currency} est disponible pour {quote.Mission.MissionNumber}.",
            nameof(MissionAdditionalQuote),
            quote.Id,
            null,
            cancellationToken,
            saveChanges: false);

        await db.SaveChangesAsync(cancellationToken);
        return MissionAdditionalQuoteWorkflowResult.Ok(ToResponse(quote, quote.Mission));
    }

    public async Task<MissionAdditionalQuoteWorkflowResult> PayByCustomerAsync(
        Guid quoteId,
        PayMissionAdditionalQuoteRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return MissionAdditionalQuoteWorkflowResult.ValidationFailed("Le numero de telephone client est obligatoire.");
        }

        var quote = await db.MissionAdditionalQuotes
            .Include(item => item.Mission)
            .FirstOrDefaultAsync(item => item.Id == quoteId, cancellationToken);
        if (quote?.Mission is null)
        {
            return MissionAdditionalQuoteWorkflowResult.NotFound("Devis complementaire introuvable.");
        }

        var customer = await db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == quote.Mission.CustomerId, cancellationToken);
        if (customer is null)
        {
            return MissionAdditionalQuoteWorkflowResult.NotFound("Client introuvable.");
        }

        if (!string.Equals(customer.PhoneNumber, request.PhoneNumber.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return MissionAdditionalQuoteWorkflowResult.Forbidden("Ce numero ne correspond pas au client de la mission.");
        }

        try
        {
            quote.MarkPaid(request.PaymentReference);
        }
        catch (InvalidOperationException exception)
        {
            return MissionAdditionalQuoteWorkflowResult.ValidationFailed(exception.Message);
        }

        var amount = quote.Amount ?? 0;
        db.MissionPaymentMilestones.Add(CreatePaidMilestone(quote, request.PaymentReference));
        db.MissionFinancialBreakdowns.Add(new MissionFinancialBreakdown(
            quote.MissionId,
            MissionFinancialLineType.AdditionalQuote,
            $"Devis complementaire - {quote.CompanyDescription}",
            amount,
            quote.Currency,
            40));

        companyNotifications.AddForMission(
            quote.Mission,
            "MissionAdditionalQuotePaid",
            $"Complement paye pour {quote.Mission.MissionNumber}",
            $"Le client a paye le devis complementaire de {amount:N0} {quote.Currency}.",
            "success",
            $"missions/{quote.Mission.Id}");

        await mobilePushNotifications.QueueForOwnerAsync(
            MobileDeviceOwnerType.Provider,
            quote.ProviderId,
            "Complement accepte",
            $"Le client a paye le devis complementaire pour {quote.Mission.MissionNumber}.",
            nameof(MissionAdditionalQuote),
            quote.Id,
            null,
            cancellationToken,
            saveChanges: false);

        await db.SaveChangesAsync(cancellationToken);
        return MissionAdditionalQuoteWorkflowResult.Ok(ToResponse(quote, quote.Mission));
    }

    private static MissionPaymentMilestone CreatePaidMilestone(MissionAdditionalQuote quote, string? paymentReference)
    {
        var milestone = new MissionPaymentMilestone(
            quote.MissionId,
            MissionPaymentMilestoneTrigger.AdditionalQuote,
            quote.Amount ?? 0,
            quote.Currency,
            $"Paiement devis complementaire {quote.Id:N}",
            40);
        milestone.MarkDue(DateTimeOffset.UtcNow);
        milestone.MarkPaid(paymentReference);
        return milestone;
    }

    private static MissionAdditionalQuoteResponse ToResponse(MissionAdditionalQuote quote, Mission mission)
    {
        return new MissionAdditionalQuoteResponse(
            quote.Id,
            quote.MissionId,
            mission.MissionNumber,
            quote.Status.ToString(),
            quote.Reason,
            quote.RequestedPhotoStoragePath,
            quote.Amount,
            quote.Currency,
            quote.CompanyDescription,
            quote.RequestedAt,
            quote.SubmittedAt,
            quote.PaidAt);
    }
}

public sealed record MissionAdditionalQuoteWorkflowResult(
    MissionAdditionalQuoteWorkflowStatus Status,
    MissionAdditionalQuoteResponse? Response,
    string? Message)
{
    public bool IsSuccess => Status == MissionAdditionalQuoteWorkflowStatus.Ok;

    public static MissionAdditionalQuoteWorkflowResult Ok(MissionAdditionalQuoteResponse response)
        => new(MissionAdditionalQuoteWorkflowStatus.Ok, response, null);

    public static MissionAdditionalQuoteWorkflowResult NotFound(string message)
        => new(MissionAdditionalQuoteWorkflowStatus.NotFound, null, message);

    public static MissionAdditionalQuoteWorkflowResult Forbidden(string message)
        => new(MissionAdditionalQuoteWorkflowStatus.Forbidden, null, message);

    public static MissionAdditionalQuoteWorkflowResult ValidationFailed(string message)
        => new(MissionAdditionalQuoteWorkflowStatus.ValidationFailed, null, message);
}

public enum MissionAdditionalQuoteWorkflowStatus
{
    Ok,
    NotFound,
    Forbidden,
    ValidationFailed
}
