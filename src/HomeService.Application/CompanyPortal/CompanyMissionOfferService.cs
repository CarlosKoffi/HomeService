using HomeService.Application.Abstractions;
using HomeService.Application.Missions;
using HomeService.Application.Notifications;
using HomeService.Contracts.CompanyPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.CompanyPortal;

public sealed class CompanyMissionOfferService(
    IAppDbContext db,
    CustomerMissionProgressNotificationService? customerNotifications = null)
{
    private static readonly TimeSpan ProviderAssignmentWindow = TimeSpan.FromMinutes(10);

    public async Task<CompanyMissionOfferListResult> ListOpenOffersAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var company = await db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == companyId && item.Status != CompanyStatus.Suspended, cancellationToken);
        if (company is null)
        {
            return CompanyMissionOfferListResult.NotFound();
        }

        var now = DateTimeOffset.UtcNow;
        var missions = await (
                from mission in db.Missions.AsNoTracking()
                join service in db.Services.AsNoTracking() on mission.ServiceId equals service.Id
                join customer in db.Customers.AsNoTracking() on mission.CustomerId equals customer.Id
                where mission.CompanyId == null
                    && (mission.Status == MissionStatus.SearchingProvider || mission.Status == MissionStatus.Offered)
                orderby mission.CreatedAt descending
                select new
                {
                    Mission = mission,
                    ServiceName = service.Name,
                    CustomerName = customer.FirstName + " " + customer.LastName,
                    customer.PhoneNumber
                })
            .Take(40)
            .ToListAsync(cancellationToken);

        var missionIds = missions.Select(item => item.Mission.Id).ToList();
        var dispatchOffers = await db.MissionDispatchOffers
            .AsNoTracking()
            .Where(offer => missionIds.Contains(offer.MissionId))
            .ToListAsync(cancellationToken);
        var providerSkills = await (
                from skill in db.ProviderServices.AsNoTracking()
                join provider in db.Providers.AsNoTracking() on skill.ProviderId equals provider.Id
                where skill.CompanyId == companyId
                    && skill.IsActive
                    && provider.Status == ProviderStatus.Approved
                select new
            {
                skill.ServiceId,
                Prestations = skill.Prestations
                    .Where(prestation => prestation.IsActive)
                    .Select(prestation => prestation.ServicePrestationId)
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var offers = missions
            .Select(item =>
            {
                var companyOffer = dispatchOffers
                    .Where(offer => offer.MissionId == item.Mission.Id && offer.CompanyId == companyId)
                    .OrderByDescending(offer => offer.CreatedAt)
                    .FirstOrDefault();
                var hasCompatibleProvider = providerSkills.Any(skill =>
                    skill.ServiceId == item.Mission.ServiceId
                    && (item.Mission.ServicePrestationId == null
                        || skill.Prestations.Contains(item.Mission.ServicePrestationId.Value)));
                var canAccept = company.Status == CompanyStatus.Approved
                    && hasCompatibleProvider
                    && companyOffer?.Status == MissionDispatchOfferStatus.Sent
                    && companyOffer.ExpiresAt > now;
                var accessState = canAccept
                    ? "Available"
                    : company.Status != CompanyStatus.Approved
                        ? "CompanyInactive"
                        : !hasCompatibleProvider
                            ? "MissingSkill"
                            : companyOffer is null
                                ? "WaitingPriority"
                                : companyOffer.Status.ToString();
                var accessMessage = accessState switch
                {
                    "Available" => $"Disponible maintenant - votre entreprise est au rang {companyOffer!.Rank}.",
                    "CompanyInactive" => "Votre entreprise doit etre validee avant de pouvoir accepter une demande.",
                    "MissingSkill" => "Aucun prestataire actif de votre equipe ne maitrise ce service ou cette prestation.",
                    "WaitingPriority" => $"Votre priorite est {company.MissionDispatchPriority}. Les entreprises mieux classees ont encore la priorite.",
                    nameof(MissionDispatchOfferStatus.Sent) => "Le delai accorde a votre entreprise pour accepter cette demande est expire.",
                    nameof(MissionDispatchOfferStatus.Accepted) => "Cette demande a deja ete acceptee par votre entreprise.",
                    nameof(MissionDispatchOfferStatus.Expired) => "Le delai accorde a votre entreprise pour accepter cette demande est expire.",
                    nameof(MissionDispatchOfferStatus.Lost) => "Une autre entreprise a accepte cette demande avant vous.",
                    nameof(MissionDispatchOfferStatus.Cancelled) => "Cette demande a ete annulee par le client ou la plateforme.",
                    nameof(MissionDispatchOfferStatus.AssignmentTimedOut) => "Le delai d'affectation d'un prestataire est depasse.",
                    _ => "Cette demande n'est actuellement pas accessible a votre entreprise."
                };

                return new CompanyMissionOfferResponse(
                    companyOffer?.Id,
                    item.Mission.Id,
                    item.Mission.MissionNumber,
                    item.ServiceName,
                    item.CustomerName,
                    item.PhoneNumber,
                    companyOffer?.Status.ToString() ?? "Visible",
                    companyOffer?.ExpiresAt ?? now,
                    item.Mission.ServiceAddress,
                    item.Mission.Description,
                    item.Mission.EstimatedDurationMinutes,
                    item.Mission.ScheduledFor,
                    companyOffer?.Rank,
                    companyOffer?.Score,
                    canAccept,
                    hasCompatibleProvider,
                    accessState,
                    accessMessage,
                    company.MissionDispatchPriority);
            })
            .OrderByDescending(offer => offer.CanAccept)
            .ThenBy(offer => offer.Rank ?? int.MaxValue)
            .ThenBy(offer => offer.ScheduledFor ?? DateTimeOffset.MaxValue)
            .Take(10)
            .ToList();

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
            var assignmentWindow = await MissionWorkflowSettingsResolver.ResolveMinutesAsync(
                db,
                MissionWorkflowSettingsResolver.CompanyProviderAssignmentMinutes,
                (int)ProviderAssignmentWindow.TotalMinutes,
                cancellationToken);
            mission.AcceptCompanyOffer(companyId, now.Add(assignmentWindow));
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

        if (customerNotifications is not null)
        {
            await customerNotifications.NotifyCompanyAnalyzingAsync(
                mission,
                companyId,
                cancellationToken);
        }

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
