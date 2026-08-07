using HomeService.Application.Abstractions;
using HomeService.Application.Missions;
using HomeService.Contracts.Clients;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Clients;

public sealed class ClientMissionStatusService(
    IAppDbContext db,
    MissionCommercialPricingService? commercialPricing = null)
{
    public async Task<ClientMissionStatusResult> GetAsync(
        Guid missionId,
        string customerPhoneNumber,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(customerPhoneNumber))
        {
            return ClientMissionStatusResult.Forbidden("Le numero client est obligatoire pour consulter la mission.");
        }

        var mission = await db.Missions
            .AsNoTracking()
            .Include(item => item.CustomerPaymentMethod)
            .FirstOrDefaultAsync(item => item.Id == missionId, cancellationToken);
        if (mission is null)
        {
            return ClientMissionStatusResult.NotFound("Mission introuvable.");
        }

        var customer = await db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == mission.CustomerId, cancellationToken);
        if (customer is null)
        {
            return ClientMissionStatusResult.NotFound("Client introuvable.");
        }

        if (!PhoneMatches(customer.PhoneNumber, customerPhoneNumber))
        {
            return ClientMissionStatusResult.Forbidden("Cette mission n'est pas rattachee a ce numero.");
        }

        var service = await db.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == mission.ServiceId, cancellationToken);
        var prestation = mission.ServicePrestationId is null
            ? null
            : await db.ServicePrestations
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == mission.ServicePrestationId.Value, cancellationToken);
        var option = mission.ServiceOptionId is null
            ? null
            : await db.ServiceOptions
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == mission.ServiceOptionId.Value, cancellationToken);

        var assignedCompany = mission.CompanyId is null
            ? null
            : await db.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == mission.CompanyId.Value, cancellationToken);
        var assignedProvider = mission.ProviderId is null
            ? null
            : await db.Providers
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == mission.ProviderId.Value, cancellationToken);

        var providerPhoto = assignedProvider is null
            ? null
            : await db.ProviderDocuments
                .AsNoTracking()
                .Where(document => document.ProviderId == assignedProvider.Id && document.DocumentType == ProviderDocumentType.Photo)
                .OrderByDescending(document => document.UpdatedAt ?? document.CreatedAt)
                .Select(document => document.StoragePath)
                .FirstOrDefaultAsync(cancellationToken);
        var providerRating = assignedProvider is null
            ? null
            : await db.MissionReviews
                .AsNoTracking()
                .Where(review => review.ProviderId == assignedProvider.Id)
                .AverageAsync(review => (decimal?)review.OverallRating, cancellationToken);
        var providerCompletedMissionCount = assignedProvider is null
            ? 0
            : await db.Missions
                .AsNoTracking()
                .CountAsync(item => item.ProviderId == assignedProvider.Id
                    && item.Status == MissionStatus.Completed
                    && item.CustomerCompletionValidatedAt != null, cancellationToken);

        var offerRows = await db.MissionDispatchOffers
            .AsNoTracking()
            .Where(offer => offer.MissionId == mission.Id)
            .Join(
                db.Companies.AsNoTracking(),
                offer => offer.CompanyId,
                company => company.Id,
                (offer, company) => new { Offer = offer, Company = company })
            .OrderBy(row => row.Offer.Rank)
            .ToListAsync(cancellationToken);
        var offers = offerRows
            .Select(row => new ClientMissionOfferResponse(
                row.Offer.Id,
                row.Company.Id,
                row.Company.Name,
                row.Offer.Rank,
                row.Offer.Score,
                row.Offer.Status.ToString(),
                row.Offer.ExpiresAt,
                row.Offer.RespondedAt))
            .ToList();

        var photos = await db.MissionAttachments
            .AsNoTracking()
            .Where(attachment => attachment.MissionId == mission.Id
                && attachment.AttachmentType == MissionAttachmentType.CustomerPhoto
                && !attachment.IsDeleted)
            .OrderBy(attachment => attachment.CreatedAt)
            .Select(attachment => new ClientMissionAttachmentResponse(
                attachment.Id,
                attachment.OriginalFileName,
                attachment.StoragePath,
                attachment.ContentType,
                attachment.FileSizeBytes,
                attachment.Caption))
            .ToListAsync(cancellationToken);

        var additionalQuoteRows = await db.MissionAdditionalQuotes
            .AsNoTracking()
            .Where(quote => quote.MissionId == mission.Id)
            .OrderByDescending(quote => quote.RequestedAt)
            .ToListAsync(cancellationToken);
        var additionalQuotes = additionalQuoteRows
            .Select(quote => new ClientMissionAdditionalQuoteResponse(
                quote.Id,
                quote.Status.ToString(),
                quote.Reason,
                quote.RequestedPhotoStoragePath,
                quote.Amount,
                quote.Currency,
                quote.CompanyDescription,
                quote.RequestedAt,
                quote.SubmittedAt,
                quote.PaidAt,
                quote.Status == MissionAdditionalQuoteStatus.Submitted && quote.Amount > 0))
            .ToList();

        var priceRange = ResolvePriceRange(service, prestation, option);
        var quotedAmount = mission.CompanyQuotedAmount ?? mission.FinalTotalAmount ?? mission.EstimatedTotalAmount ?? 0;
        var pricing = quotedAmount > 0 && mission.CompanyId.HasValue
            ? await (commercialPricing ?? new MissionCommercialPricingService(db))
                .CalculateAsync(mission, quotedAmount, cancellationToken)
            : new MissionCommercialPricing(
                quotedAmount,
                Math.Clamp(mission.PartsEstimateAmount.GetValueOrDefault(), 0, quotedAmount),
                Math.Max(0, quotedAmount - mission.PartsEstimateAmount.GetValueOrDefault()),
                0,
                0,
                0,
                0,
                quotedAmount,
                quotedAmount,
                true,
                mission.Currency);
        var response = new ClientMissionStatusResponse(
            mission.Id,
            mission.MissionNumber,
            mission.Status.ToString(),
            mission.QuoteStatus.ToString(),
            mission.PaymentStatus.ToString(),
            mission.Mode.ToString(),
            mission.PaymentMethod.ToString(),
            mission.CustomerPaymentMethodId,
            mission.CustomerPaymentMethod?.Label,
            mission.CustomerPaymentMethod?.MaskedReference,
            service?.Name,
            prestation?.Name,
            option?.Name,
            mission.Description,
            mission.ServiceAddress,
            mission.CreatedAt,
            mission.ScheduledFor,
            mission.CompanyQuotedAt,
            mission.ProviderAcceptedAt,
            mission.CustomerPaymentExpiresAt,
            mission.CustomerConfirmedAt,
            mission.CustomerCompletionValidationExpiresAt,
            mission.CustomerCompletionValidatedAt,
            priceRange.StartingPriceAmount,
            priceRange.MaximumPriceAmount,
            mission.EstimatedTotalAmount,
            mission.CompanyQuotedAmount,
            mission.PartsEstimateAmount,
            mission.PartsDescription,
            mission.FinalTotalAmount,
            pricing.ServiceAmount,
            pricing.CustomerServiceFeeAmount,
            pricing.CustomerServiceFeeRateBasisPoints,
            pricing.CustomerTotalAmount,
            mission.TransportFeeAmount,
            mission.Currency,
            mission.RequiresCompanyQuote,
            mission.CanRevealContactDetails,
            assignedCompany is null
                ? null
                : new ClientMissionCompanyResponse(
                    assignedCompany.Id,
                    assignedCompany.Name,
                    mission.CanRevealContactDetails ? assignedCompany.PhoneNumber : null,
                    mission.CanRevealContactDetails ? assignedCompany.Email : null),
            assignedProvider is null
                ? null
                : BuildProviderResponse(
                    mission,
                    assignedProvider.Id,
                    assignedProvider.FullName,
                    mission.CanRevealContactDetails ? assignedProvider.PhoneNumber : null,
                    providerPhoto,
                    providerRating,
                    providerCompletedMissionCount,
                    assignedProvider.CurrentLatitude ?? assignedProvider.MissionLatitude,
                    assignedProvider.CurrentLongitude ?? assignedProvider.MissionLongitude),
            offers,
            additionalQuotes,
            photos,
            BuildActions(mission, pricing.CustomerTotalAmount),
            BuildMessage(mission));

        return ClientMissionStatusResult.Ok(response);
    }

    private static ClientMissionStatusPriceRange ResolvePriceRange(
        Domain.Entities.Service? service,
        Domain.Entities.ServicePrestation? prestation,
        Domain.Entities.ServiceOption? option)
    {
        if (option is not null)
        {
            return new ClientMissionStatusPriceRange(option.PriceMinAmount, option.PriceMaxAmount);
        }

        if (prestation is not null)
        {
            return new ClientMissionStatusPriceRange(prestation.PriceMinAmount, prestation.PriceMaxAmount);
        }

        if (service is not null)
        {
            return new ClientMissionStatusPriceRange(service.PriceMinAmount, service.PriceMaxAmount);
        }

        return new ClientMissionStatusPriceRange(0, 0);
    }

    private static ClientMissionAvailableActionsResponse BuildActions(
        Domain.Entities.Mission mission,
        int customerTotalAmount)
    {
        var paymentActionIsAvailable = mission.Status == MissionStatus.Accepted
            && mission.QuoteStatus == MissionQuoteStatus.Submitted
            && mission.CompanyQuotedAmount is > 0
            && mission.CustomerConfirmedAt is null;
        var canAcceptQuote = paymentActionIsAvailable
            && mission.CustomerPaymentMethodId.HasValue;
        var canCancel = mission.Status is not (MissionStatus.Started or MissionStatus.Cancelled or MissionStatus.Completed or MissionStatus.Disputed or MissionStatus.Resolved);
        var canCall = mission.CanRevealContactDetails;
        var canValidateCompletion = mission.Status == MissionStatus.Completed
            && mission.CustomerCompletionValidatedAt is null;
        var canOpenDispute = mission.Status is MissionStatus.Started or MissionStatus.Completed
            && mission.CustomerCompletionValidatedAt is null;
        var canRateMission = mission.Status == MissionStatus.Completed
            && mission.CustomerCompletionValidatedAt is null;

        return new ClientMissionAvailableActionsResponse(
            canAcceptQuote,
            canCancel,
            canCall && mission.CompanyId is not null,
            canCall && mission.ProviderId is not null,
            canValidateCompletion,
            canRateMission,
            canOpenDispute,
            paymentActionIsAvailable && !mission.CustomerPaymentMethodId.HasValue,
            canAcceptQuote ? customerTotalAmount : null,
            BuildPrimaryAction(canAcceptQuote, canValidateCompletion, canCall, canCancel));
    }

    private static string? BuildPrimaryAction(
        bool canAcceptQuote,
        bool canValidateCompletion,
        bool canCall,
        bool canCancel)
    {
        if (canAcceptQuote)
        {
            return "AcceptQuote";
        }

        if (canValidateCompletion)
        {
            return "ValidateCompletion";
        }

        if (canCall)
        {
            return "CallProvider";
        }

        return canCancel ? "CancelMission" : null;
    }

    private static string BuildMessage(Domain.Entities.Mission mission)
    {
        var status = mission.Status;
        var quoteStatus = mission.QuoteStatus;
        var paymentStatus = mission.PaymentStatus;

        if (status == MissionStatus.Cancelled)
        {
            return "Votre demande est annulee.";
        }

        if (status == MissionStatus.Disputed)
        {
            return "Un litige est ouvert sur cette mission. Notre equipe suit le dossier.";
        }

        if (status == MissionStatus.Completed && paymentStatus != PaymentStatus.Paid)
        {
            return "La mission est terminee. Vous pouvez confirmer que tout est conforme.";
        }

        if (status == MissionStatus.Completed)
        {
            return "Mission terminee et paiement finalise.";
        }

        if (status == MissionStatus.Accepted && paymentStatus == PaymentStatus.Pending)
        {
            return "Le prestataire a confirme la mission. Validez le prix et payez pour lancer l'intervention.";
        }

        if (status is MissionStatus.OnTheWay or MissionStatus.Started)
        {
            return "Votre technicien est affecte. Les informations utiles sont disponibles.";
        }

        if (quoteStatus == MissionQuoteStatus.Submitted)
        {
            return "Une entreprise a propose un prix. Vous pouvez accepter pour confirmer la mission.";
        }

        if (status == MissionStatus.Assigned)
        {
            return "Une entreprise prepare votre intervention.";
        }

        if (mission.CompanyId is not null && status is MissionStatus.Offered or MissionStatus.SearchingProvider)
        {
            return "Une entreprise analyse votre demande et prepare l'affectation d'un technicien.";
        }

        if (status is MissionStatus.Offered or MissionStatus.SearchingProvider)
        {
            return "Votre demande est transmise aux entreprises disponibles.";
        }

        return "Votre demande est enregistree.";
    }

    private static ClientMissionProviderResponse BuildProviderResponse(
        Domain.Entities.Mission mission,
        Guid providerId,
        string fullName,
        string? phoneNumber,
        string? photoStoragePath,
        decimal? averageRating,
        int completedMissionCount,
        decimal? providerLatitude,
        decimal? providerLongitude)
    {
        var canTrack = mission.IsInitialPaymentConfirmed
            && mission.Status is MissionStatus.Accepted or MissionStatus.OnTheWay
            && providerLatitude.HasValue
            && providerLongitude.HasValue
            && mission.ServiceLatitude.HasValue
            && mission.ServiceLongitude.HasValue;
        decimal? distanceKm = canTrack
            ? CalculateDistanceKm(providerLatitude!.Value, providerLongitude!.Value, mission.ServiceLatitude!.Value, mission.ServiceLongitude!.Value)
            : null;
        int? etaMinutes = distanceKm.HasValue
            ? Math.Clamp((int)Math.Ceiling(distanceKm.Value * 1.25m / 20m * 60m), 2, 180)
            : null;

        return new ClientMissionProviderResponse(
            providerId,
            fullName,
            phoneNumber,
            photoStoragePath,
            averageRating,
            completedMissionCount,
            etaMinutes,
            canTrack ? providerLatitude : null,
            canTrack ? providerLongitude : null,
            canTrack ? mission.ServiceLatitude : null,
            canTrack ? mission.ServiceLongitude : null,
            distanceKm,
            canTrack);
    }

    private static decimal CalculateDistanceKm(decimal latitude1, decimal longitude1, decimal latitude2, decimal longitude2)
    {
        const double earthRadiusKm = 6371d;
        var latitudeDelta = DegreesToRadians((double)(latitude2 - latitude1));
        var longitudeDelta = DegreesToRadians((double)(longitude2 - longitude1));
        var firstLatitude = DegreesToRadians((double)latitude1);
        var secondLatitude = DegreesToRadians((double)latitude2);
        var value = Math.Sin(latitudeDelta / 2) * Math.Sin(latitudeDelta / 2)
            + Math.Cos(firstLatitude) * Math.Cos(secondLatitude)
            * Math.Sin(longitudeDelta / 2) * Math.Sin(longitudeDelta / 2);
        var distance = earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(value), Math.Sqrt(1 - value));
        return Math.Round((decimal)distance, 1, MidpointRounding.AwayFromZero);
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;

    private sealed record ClientMissionStatusPriceRange(int StartingPriceAmount, int MaximumPriceAmount);

    private static bool PhoneMatches(string storedPhoneNumber, string providedPhoneNumber)
    {
        return NormalizePhone(storedPhoneNumber) == NormalizePhone(providedPhoneNumber);
    }

    private static string NormalizePhone(string phoneNumber)
    {
        return new string(phoneNumber.Where(char.IsDigit).ToArray());
    }
}

public sealed record ClientMissionStatusResult(
    ClientMissionStatusResultStatus Status,
    ClientMissionStatusResponse? Response,
    string Message)
{
    public bool IsSuccess => Status == ClientMissionStatusResultStatus.Success;

    public static ClientMissionStatusResult Ok(ClientMissionStatusResponse response)
    {
        return new ClientMissionStatusResult(ClientMissionStatusResultStatus.Success, response, string.Empty);
    }

    public static ClientMissionStatusResult NotFound(string message)
    {
        return new ClientMissionStatusResult(ClientMissionStatusResultStatus.NotFound, null, message);
    }

    public static ClientMissionStatusResult Forbidden(string message)
    {
        return new ClientMissionStatusResult(ClientMissionStatusResultStatus.Forbidden, null, message);
    }
}

public enum ClientMissionStatusResultStatus
{
    Success = 0,
    NotFound = 1,
    Forbidden = 2
}
