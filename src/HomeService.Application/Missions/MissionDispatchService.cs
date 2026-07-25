using HomeService.Application.Abstractions;
using HomeService.Domain.Common;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Missions;

public sealed class MissionDispatchService(
    IAppDbContext db,
    MissionDispatchScoringService scoringService)
{
    private static readonly TimeSpan DefaultCompanyResponseWindow = TimeSpan.FromMinutes(5);

    public async Task<MissionDispatchCreationResult> CreateInitialOffersAsync(
        Guid missionId,
        bool isUrgent,
        CancellationToken cancellationToken)
    {
        var mission = await db.Missions
            .FirstOrDefaultAsync(item => item.Id == missionId, cancellationToken);

        if (mission is null)
        {
            return MissionDispatchCreationResult.Failed("Mission introuvable.");
        }

        var existingOpenOffers = await db.MissionDispatchOffers
            .Where(offer => offer.MissionId == missionId && offer.Status == MissionDispatchOfferStatus.Sent)
            .OrderBy(offer => offer.Rank)
            .ToListAsync(cancellationToken);

        if (existingOpenOffers.Count > 0)
        {
            return MissionDispatchCreationResult.Ok(existingOpenOffers);
        }

        var request = new MissionDispatchRequest(
            mission.Id,
            mission.ServiceId,
            mission.ServicePrestationId,
            mission.ServiceAddress,
            isUrgent);

        var candidates = await GetCandidatesAsync(mission, cancellationToken);
        var scores = scoringService.SelectTopCompanies(request, candidates);

        if (scores.Count == 0)
        {
            return MissionDispatchCreationResult.Failed("Aucune entreprise eligible trouvee pour cette mission.");
        }

        var expiresAt = DateTimeOffset.UtcNow.Add(DefaultCompanyResponseWindow);
        var offers = scores
            .Select(score => new MissionDispatchOffer(
                mission.Id,
                score.CompanyId,
                score.Rank,
                score.Score,
                score.Details,
                expiresAt))
            .ToList();

        foreach (var offer in offers)
        {
            db.MissionDispatchOffers.Add(offer);
        }

        await db.SaveChangesAsync(cancellationToken);
        return MissionDispatchCreationResult.Ok(offers);
    }

    public async Task<IReadOnlyList<MissionDispatchCandidate>> GetCandidatesAsync(
        Mission mission,
        CancellationToken cancellationToken)
    {
        var service = await db.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == mission.ServiceId, cancellationToken);

        if (service is null)
        {
            return [];
        }

        var providerCompanyIds = await db.ProviderServices
            .AsNoTracking()
            .Where(providerService => providerService.ServiceId == mission.ServiceId && providerService.IsActive)
            .Select(providerService => providerService.CompanyId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var normalizedServiceName = service.NormalizedName;
        var companies = await db.Companies
            .AsNoTracking()
            .Where(company => company.Status == CompanyStatus.Approved)
            .ToListAsync(cancellationToken);

        var eligibleCompanies = companies
            .Where(company => providerCompanyIds.Contains(company.Id)
                || ContainsPlannedService(company.PlannedServices, normalizedServiceName))
            .ToList();

        if (eligibleCompanies.Count == 0)
        {
            return [];
        }

        var companyIds = eligibleCompanies.Select(company => company.Id).ToHashSet();
        var recentFrom = DateTimeOffset.UtcNow.AddDays(-14);

        var missionStats = await db.Missions
            .AsNoTracking()
            .Where(item => item.CompanyId.HasValue && companyIds.Contains(item.CompanyId.Value))
            .GroupBy(item => item.CompanyId!.Value)
            .Select(group => new
            {
                CompanyId = group.Key,
                Completed = group.Count(item => item.Status == MissionStatus.Completed),
                Recent = group.Count(item => item.CreatedAt >= recentFrom),
                Cancelled = group.Count(item => item.Status == MissionStatus.Cancelled),
                AverageQuote = group
                    .Where(item => item.CompanyQuotedAmount.HasValue)
                    .Average(item => (decimal?)item.CompanyQuotedAmount)
            })
            .ToListAsync(cancellationToken);

        var marketAverage = await db.Missions
            .AsNoTracking()
            .Where(item => item.ServiceId == mission.ServiceId && item.CompanyQuotedAmount.HasValue)
            .AverageAsync(item => (decimal?)item.CompanyQuotedAmount, cancellationToken);

        return eligibleCompanies
            .Select(company =>
            {
                var stats = missionStats.FirstOrDefault(item => item.CompanyId == company.Id);
                return new MissionDispatchCandidate(
                    company.Id,
                    company.Name,
                    company.MissionDispatchPriority,
                    CoversRequestedZone(company.InterventionZones, mission.ServiceAddress),
                    company.AcceptsUrgentMissions,
                    AverageRating: null,
                    stats?.Completed ?? 0,
                    stats?.Recent ?? 0,
                    stats?.Cancelled ?? 0,
                    NoResponseCount: 0,
                    CalculatePriceDeviation(stats?.AverageQuote, marketAverage));
            })
            .ToList();
    }

    private static bool ContainsPlannedService(string? plannedServices, string normalizedServiceName)
    {
        if (string.IsNullOrWhiteSpace(plannedServices) || string.IsNullOrWhiteSpace(normalizedServiceName))
        {
            return false;
        }

        return CatalogNameNormalizer.Normalize(plannedServices).Contains(normalizedServiceName, StringComparison.Ordinal);
    }

    private static bool CoversRequestedZone(string? interventionZones, string? missionAddress)
    {
        if (string.IsNullOrWhiteSpace(missionAddress))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(interventionZones))
        {
            return false;
        }

        var normalizedZones = CatalogNameNormalizer.Normalize(interventionZones);
        return CatalogNameNormalizer.Normalize(missionAddress)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(part => part.Length >= 4 && normalizedZones.Contains(part, StringComparison.Ordinal));
    }

    private static decimal? CalculatePriceDeviation(decimal? companyAverage, decimal? marketAverage)
    {
        if (companyAverage is null || marketAverage is null or <= 0)
        {
            return null;
        }

        return ((companyAverage.Value - marketAverage.Value) / marketAverage.Value) * 100m;
    }
}
