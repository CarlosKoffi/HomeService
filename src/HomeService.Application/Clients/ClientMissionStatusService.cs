using HomeService.Application.Abstractions;
using HomeService.Contracts.Clients;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Clients;

public sealed class ClientMissionStatusService(IAppDbContext db)
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

        var offers = await db.MissionDispatchOffers
            .AsNoTracking()
            .Where(offer => offer.MissionId == mission.Id)
            .Join(
                db.Companies.AsNoTracking(),
                offer => offer.CompanyId,
                company => company.Id,
                (offer, company) => new ClientMissionOfferResponse(
                    offer.Id,
                    company.Id,
                    company.Name,
                    offer.Rank,
                    offer.Score,
                    offer.Status.ToString(),
                    offer.ExpiresAt,
                    offer.RespondedAt))
            .OrderBy(offer => offer.Rank)
            .ToListAsync(cancellationToken);

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

        var response = new ClientMissionStatusResponse(
            mission.Id,
            mission.MissionNumber,
            mission.Status.ToString(),
            mission.QuoteStatus.ToString(),
            mission.PaymentStatus.ToString(),
            mission.Mode.ToString(),
            mission.PaymentMethod.ToString(),
            service?.Name,
            prestation?.Name,
            mission.Description,
            mission.ServiceAddress,
            mission.CreatedAt,
            mission.ScheduledFor,
            mission.CompanyQuotedAt,
            mission.ProviderAcceptedAt,
            mission.CustomerConfirmedAt,
            mission.CustomerCompletionValidatedAt,
            mission.EstimatedTotalAmount,
            mission.CompanyQuotedAmount,
            mission.PartsEstimateAmount,
            mission.PartsDescription,
            mission.FinalTotalAmount,
            mission.PlatformCommissionAmount,
            mission.CompanyPayoutAmount,
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
                : new ClientMissionProviderResponse(
                    assignedProvider.Id,
                    assignedProvider.FullName,
                    mission.CanRevealContactDetails ? assignedProvider.PhoneNumber : null,
                    providerPhoto),
            offers,
            photos,
            BuildMessage(mission.Status, mission.QuoteStatus, mission.PaymentStatus));

        return ClientMissionStatusResult.Ok(response);
    }

    private static string BuildMessage(MissionStatus status, MissionQuoteStatus quoteStatus, PaymentStatus paymentStatus)
    {
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

        if (status is MissionStatus.Accepted or MissionStatus.OnTheWay or MissionStatus.Started)
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

        if (status is MissionStatus.Offered or MissionStatus.SearchingProvider)
        {
            return "Votre demande est transmise aux entreprises disponibles.";
        }

        return "Votre demande est enregistree.";
    }

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
