using HomeService.Application.Abstractions;
using HomeService.Contracts.CompanyPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.CompanyPortal;

public sealed class CompanyMissionOfferService(IAppDbContext db)
{
    public async Task<CompanyMissionOfferListResult> ListOpenOffersAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var companyExists = await db.Companies
            .AsNoTracking()
            .AnyAsync(company => company.Id == companyId && company.Status != CompanyStatus.Suspended, cancellationToken);
        if (!companyExists)
        {
            return CompanyMissionOfferListResult.NotFound();
        }

        var now = DateTimeOffset.UtcNow;
        var offers = await (
                from offer in db.MissionDispatchOffers.AsNoTracking()
                join mission in db.Missions.AsNoTracking() on offer.MissionId equals mission.Id
                join service in db.Services.AsNoTracking() on mission.ServiceId equals service.Id
                join customer in db.Customers.AsNoTracking() on mission.CustomerId equals customer.Id
                where offer.CompanyId == companyId
                    && offer.Status == MissionDispatchOfferStatus.Sent
                    && offer.ExpiresAt > now
                orderby offer.ExpiresAt, offer.Rank
                select new CompanyMissionOfferResponse(
                    offer.Id,
                    mission.Id,
                    mission.MissionNumber,
                    service.Name,
                    customer.FirstName + " " + customer.LastName,
                    customer.PhoneNumber,
                    offer.Status.ToString(),
                    offer.ExpiresAt,
                    mission.ServiceAddress,
                    mission.Description,
                    mission.EstimatedDurationMinutes,
                    mission.ScheduledFor,
                    offer.Rank,
                    offer.Score))
            .ToListAsync(cancellationToken);

        return CompanyMissionOfferListResult.Ok(offers);
    }

    public async Task<CompanyMissionOfferAcceptResult> AcceptAsync(
        Guid companyId,
        Guid offerId,
        CancellationToken cancellationToken)
    {
        var offer = await db.MissionDispatchOffers
            .Include(item => item.Mission)
            .FirstOrDefaultAsync(item => item.Id == offerId && item.CompanyId == companyId, cancellationToken);
        if (offer is null || offer.Mission is null)
        {
            return CompanyMissionOfferAcceptResult.NotFound();
        }

        var now = DateTimeOffset.UtcNow;
        if (!offer.IsOpen(now))
        {
            offer.MarkExpired(now);
            await db.SaveChangesAsync(cancellationToken);
            return CompanyMissionOfferAcceptResult.Invalid("Cette demande n'est plus disponible.");
        }

        var mission = offer.Mission;
        try
        {
            mission.AcceptCompanyOffer(companyId);
            offer.Accept(now);
        }
        catch (InvalidOperationException exception)
        {
            return CompanyMissionOfferAcceptResult.Invalid(exception.Message);
        }

        var competingOffers = await db.MissionDispatchOffers
            .Where(item => item.MissionId == mission.Id
                && item.Id != offer.Id
                && item.Status == MissionDispatchOfferStatus.Sent)
            .ToListAsync(cancellationToken);
        foreach (var competingOffer in competingOffers)
        {
            competingOffer.MarkLost();
        }

        db.CompanyPortalActivities.Add(new CompanyPortalActivity(
            companyId,
            "mission",
            "Demande acceptee",
            $"La mission {mission.MissionNumber} est maintenant dans votre portail. Affectez un prestataire pour continuer.",
            "blue",
            nameof(Mission),
            mission.Id));

        await db.SaveChangesAsync(cancellationToken);

        return CompanyMissionOfferAcceptResult.Ok(new CompanyMissionOfferAcceptResponse(
            mission.Id,
            mission.MissionNumber,
            mission.Status.ToString(),
            "Demande acceptee. Affectez maintenant un prestataire."));
    }
}

public sealed record CompanyMissionOfferListResult(
    bool IsSuccess,
    IReadOnlyList<CompanyMissionOfferResponse> Offers,
    string? Message)
{
    public static CompanyMissionOfferListResult Ok(IReadOnlyList<CompanyMissionOfferResponse> offers)
    {
        return new CompanyMissionOfferListResult(true, offers, null);
    }

    public static CompanyMissionOfferListResult NotFound()
    {
        return new CompanyMissionOfferListResult(false, [], "Entreprise introuvable ou inactive.");
    }
}

public sealed record CompanyMissionOfferAcceptResult(
    bool IsSuccess,
    CompanyMissionOfferAcceptResponse? Response,
    string? Message,
    bool IsNotFound)
{
    public static CompanyMissionOfferAcceptResult Ok(CompanyMissionOfferAcceptResponse response)
    {
        return new CompanyMissionOfferAcceptResult(true, response, null, false);
    }

    public static CompanyMissionOfferAcceptResult Invalid(string message)
    {
        return new CompanyMissionOfferAcceptResult(false, null, message, false);
    }

    public static CompanyMissionOfferAcceptResult NotFound()
    {
        return new CompanyMissionOfferAcceptResult(false, null, "Demande introuvable.", true);
    }
}
