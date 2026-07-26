using HomeService.Application.Abstractions;
using HomeService.Contracts.Services;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminServiceCatalogInsightsService(IAppDbContext db)
{
    public async Task<ServiceCatalogInsightListResponse> GetAsync(CancellationToken cancellationToken)
    {
        var services = await db.Services
            .AsNoTracking()
            .Include(service => service.Prestations)
            .OrderBy(service => service.Name)
            .ToListAsync(cancellationToken);

        var serviceIds = services.Select(service => service.Id).ToList();
        var prestationIds = services
            .SelectMany(service => service.Prestations)
            .Select(prestation => prestation.Id)
            .ToList();

        var providerRows = await (
            from providerService in db.ProviderServices.AsNoTracking()
            join provider in db.Providers.AsNoTracking() on providerService.ProviderId equals provider.Id
            where serviceIds.Contains(providerService.ServiceId) && providerService.IsActive
            select new ServiceProviderInsightRow(
                providerService.ServiceId,
                providerService.ProviderId,
                providerService.CompanyId,
                provider.Status,
                provider.EmploymentType))
            .ToListAsync(cancellationToken);

        var providerPrestationRows = await (
            from providerPrestation in db.ProviderServicePrestations.AsNoTracking()
            join providerService in db.ProviderServices.AsNoTracking() on providerPrestation.ProviderServiceId equals providerService.Id
            join provider in db.Providers.AsNoTracking() on providerService.ProviderId equals provider.Id
            where prestationIds.Contains(providerPrestation.ServicePrestationId) && providerPrestation.IsActive
            select new PrestationProviderInsightRow(
                providerPrestation.ServicePrestationId,
                providerService.ProviderId,
                provider.Status))
            .ToListAsync(cancellationToken);

        var missionRows = await db.Missions
            .AsNoTracking()
            .Where(mission => serviceIds.Contains(mission.ServiceId))
            .Select(mission => new MissionInsightRow(
                mission.ServiceId,
                mission.ServicePrestationId,
                mission.Status,
                mission.FinalTotalAmount,
                mission.CompanyQuotedAmount,
                mission.EstimatedTotalAmount,
                mission.Currency))
            .ToListAsync(cancellationToken);

        var pendingProposalRows = await db.CompanyApplicationServices
            .AsNoTracking()
            .Where(proposal =>
                proposal.MatchStatus == CompanyApplicationServiceMatchStatus.PendingMatch
                || proposal.MatchStatus == CompanyApplicationServiceMatchStatus.NeedsAdminReview
                || proposal.MatchedServiceId == null)
            .Select(proposal => new PendingProposalInsightRow(
                proposal.Id,
                proposal.MatchedServiceId,
                proposal.MatchedServicePrestationId,
                proposal.RawName))
            .ToListAsync(cancellationToken);

        var providerByService = providerRows
            .GroupBy(row => row.ServiceId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var providerByPrestation = providerPrestationRows
            .GroupBy(row => row.ServicePrestationId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var missionsByService = missionRows
            .GroupBy(row => row.ServiceId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var missionsByPrestation = missionRows
            .Where(row => row.ServicePrestationId.HasValue)
            .GroupBy(row => row.ServicePrestationId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());
        var proposalsByMatchedPrestation = pendingProposalRows
            .Where(row => row.ServicePrestationId.HasValue)
            .GroupBy(row => row.ServicePrestationId!.Value)
            .ToDictionary(group => group.Key, group => group.Count());

        var items = services.Select(service =>
        {
            providerByService.TryGetValue(service.Id, out var serviceProviders);
            serviceProviders ??= [];
            missionsByService.TryGetValue(service.Id, out var serviceMissions);
            serviceMissions ??= [];

            var prestationItems = service.Prestations
                .OrderBy(prestation => prestation.SortOrder)
                .ThenBy(prestation => prestation.Name)
                .Select(prestation =>
                {
                    providerByPrestation.TryGetValue(prestation.Id, out var prestationProviders);
                    prestationProviders ??= [];
                    missionsByPrestation.TryGetValue(prestation.Id, out var prestationMissions);
                    prestationMissions ??= [];
                    proposalsByMatchedPrestation.TryGetValue(prestation.Id, out var pendingPrestationProposalCount);

                    var prestationProviderCount = CountApprovedProviders(prestationProviders);
                    var prestationMissionCount = prestationMissions.Count;
                    var hasPrestationProviderGap = prestationProviderCount == 0;
                    var hasPrestationDemandWithoutProviders = prestationMissionCount > 0 && prestationProviderCount == 0;

                    return new ServicePrestationCatalogInsightResponse(
                        prestation.Id,
                        prestation.Name,
                        prestationProviderCount,
                        prestationMissionCount,
                        CountCompletedMissions(prestationMissions),
                        SumCompletedRevenue(prestationMissions),
                        hasPrestationProviderGap || pendingPrestationProposalCount > 0,
                        hasPrestationDemandWithoutProviders);
                })
                .ToList();

            var activeProviderCount = CountApprovedProviders(serviceProviders);
            var missionCount = serviceMissions.Count;
            var pendingProposalCount = CountPendingProposals(service, pendingProposalRows);
            var hasProviderGap = activeProviderCount == 0;
            var hasDemandWithoutProviders = missionCount > 0 && activeProviderCount == 0;
            var recommendedAction = BuildRecommendedAction(
                pendingProposalCount,
                hasDemandWithoutProviders,
                hasProviderGap,
                serviceProviders
                    .Where(provider => provider.ProviderEmploymentType == ProviderEmploymentType.TemporaryWorker)
                    .Select(provider => provider.ProviderId)
                    .Distinct()
                    .Count(),
                serviceMissions.Count(mission => mission.Status == MissionStatus.Disputed));

            return new ServiceCatalogInsightResponse(
                service.Id,
                service.Name,
                service.Prestations.Count,
                service.Prestations.Count(prestation => prestation.IsActive),
                serviceProviders.Select(provider => provider.CompanyId).Distinct().Count(),
                activeProviderCount,
                serviceProviders
                    .Where(provider => provider.ProviderEmploymentType == ProviderEmploymentType.TemporaryWorker)
                    .Select(provider => provider.ProviderId)
                    .Distinct()
                    .Count(),
                missionCount,
                CountCompletedMissions(serviceMissions),
                serviceMissions.Count(mission => mission.Status == MissionStatus.Disputed),
                SumCompletedRevenue(serviceMissions),
                service.Currency,
                prestationItems,
                pendingProposalCount,
                hasProviderGap,
                hasDemandWithoutProviders,
                recommendedAction);
        })
        .OrderByDescending(item => item.MissionCount)
        .ThenByDescending(item => item.ActiveProviderCount)
        .ThenBy(item => item.ServiceName)
        .ToList();

        var currency = items.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Currency))?.Currency ?? "XOF";
        return new ServiceCatalogInsightListResponse(
            items,
            new ServiceCatalogInsightTotalsResponse(
                items.Count,
                items.Sum(item => item.ActiveProviderCount),
                items.Sum(item => item.InterimProviderCount),
                items.Sum(item => item.MissionCount),
                items.Sum(item => item.CompletedMissionCount),
                items.Sum(item => item.RevenueAmount),
                currency,
                items.Count(item => item.HasProviderGap),
                items.Count(item => item.HasDemandWithoutProviders),
                items.Sum(item => item.Prestations.Count(prestation => prestation.HasProviderGap)),
                items.Sum(item => item.PendingProposalCount)));
    }

    private static int CountApprovedProviders(IEnumerable<ServiceProviderInsightRow> rows)
    {
        return rows
            .Where(row => row.ProviderStatus == ProviderStatus.Approved)
            .Select(row => row.ProviderId)
            .Distinct()
            .Count();
    }

    private static int CountApprovedProviders(IEnumerable<PrestationProviderInsightRow> rows)
    {
        return rows
            .Where(row => row.ProviderStatus == ProviderStatus.Approved)
            .Select(row => row.ProviderId)
            .Distinct()
            .Count();
    }

    private static int CountCompletedMissions(IEnumerable<MissionInsightRow> rows)
    {
        return rows.Count(row => row.Status == MissionStatus.Completed);
    }

    private static int SumCompletedRevenue(IEnumerable<MissionInsightRow> rows)
    {
        return rows
            .Where(row => row.Status == MissionStatus.Completed)
            .Sum(row => row.FinalTotalAmount ?? row.CompanyQuotedAmount ?? row.EstimatedTotalAmount ?? 0);
    }

    private static int CountPendingProposals(Service service, IReadOnlyList<PendingProposalInsightRow> rows)
    {
        var normalizedServiceName = Normalize(service.Name);
        var prestationIds = service.Prestations.Select(prestation => prestation.Id).ToHashSet();
        var prestationNames = service.Prestations
            .Select(prestation => Normalize(prestation.Name))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        return rows.Count(row =>
        {
            var normalizedRawName = Normalize(row.RawName);
            return row.ServiceId == service.Id
                || (row.ServicePrestationId.HasValue && prestationIds.Contains(row.ServicePrestationId.Value))
                || normalizedRawName.Contains(normalizedServiceName, StringComparison.OrdinalIgnoreCase)
                || prestationNames.Any(prestationName => normalizedRawName.Contains(prestationName, StringComparison.OrdinalIgnoreCase));
        });
    }

    private static string BuildRecommendedAction(
        int pendingProposalCount,
        bool hasDemandWithoutProviders,
        bool hasProviderGap,
        int interimProviderCount,
        int disputedMissionCount)
    {
        if (pendingProposalCount > 0)
        {
            return "Classer les propositions";
        }

        if (hasDemandWithoutProviders)
        {
            return "Recruter des prestataires";
        }

        if (hasProviderGap)
        {
            return "Ajouter une offre";
        }

        if (disputedMissionCount > 0)
        {
            return "Verifier la qualite";
        }

        if (interimProviderCount == 0)
        {
            return "Ouvrir l'interim";
        }

        return "Surveiller";
    }

    private static string Normalize(string value)
    {
        return HomeService.Domain.Common.CatalogNameNormalizer.Normalize(value);
    }

    private sealed record ServiceProviderInsightRow(
        Guid ServiceId,
        Guid ProviderId,
        Guid CompanyId,
        ProviderStatus ProviderStatus,
        ProviderEmploymentType ProviderEmploymentType);

    private sealed record PrestationProviderInsightRow(
        Guid ServicePrestationId,
        Guid ProviderId,
        ProviderStatus ProviderStatus);

    private sealed record MissionInsightRow(
        Guid ServiceId,
        Guid? ServicePrestationId,
        MissionStatus Status,
        int? FinalTotalAmount,
        int? CompanyQuotedAmount,
        int? EstimatedTotalAmount,
        string Currency);

    private sealed record PendingProposalInsightRow(
        Guid Id,
        Guid? ServiceId,
        Guid? ServicePrestationId,
        string RawName);
}
