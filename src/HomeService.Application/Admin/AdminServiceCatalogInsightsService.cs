using HomeService.Application.Abstractions;
using HomeService.Contracts.Services;
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

                    return new ServicePrestationCatalogInsightResponse(
                        prestation.Id,
                        prestation.Name,
                        CountApprovedProviders(prestationProviders),
                        prestationMissions.Count,
                        CountCompletedMissions(prestationMissions),
                        SumCompletedRevenue(prestationMissions));
                })
                .ToList();

            return new ServiceCatalogInsightResponse(
                service.Id,
                service.Name,
                service.Prestations.Count,
                service.Prestations.Count(prestation => prestation.IsActive),
                serviceProviders.Select(provider => provider.CompanyId).Distinct().Count(),
                CountApprovedProviders(serviceProviders),
                serviceProviders
                    .Where(provider => provider.ProviderEmploymentType == ProviderEmploymentType.TemporaryWorker)
                    .Select(provider => provider.ProviderId)
                    .Distinct()
                    .Count(),
                serviceMissions.Count,
                CountCompletedMissions(serviceMissions),
                serviceMissions.Count(mission => mission.Status == MissionStatus.Disputed),
                SumCompletedRevenue(serviceMissions),
                service.Currency,
                prestationItems);
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
                currency));
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
}
