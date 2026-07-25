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

        var candidates = await GetCandidatesAsync(mission, excludedCompanyIds: EmptyCompanySet, cancellationToken);
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

    public async Task<MissionDispatchReissueBatchResult> ExpireAndReissueDueOffersAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var missionIds = await db.MissionDispatchOffers
            .AsNoTracking()
            .Where(offer => offer.Status == MissionDispatchOfferStatus.Sent && offer.ExpiresAt <= now)
            .OrderBy(offer => offer.ExpiresAt)
            .Select(offer => offer.MissionId)
            .Distinct()
            .Take(Math.Clamp(batchSize, 1, 200))
            .ToListAsync(cancellationToken);

        var results = new List<MissionDispatchReissueResult>();
        foreach (var missionId in missionIds)
        {
            results.Add(await ExpireAndReissueMissionOffersAsync(missionId, now, cancellationToken));
        }

        return new MissionDispatchReissueBatchResult(results);
    }

    public async Task<MissionDispatchReissueResult> ExpireAndReissueMissionOffersAsync(
        Guid missionId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var mission = await db.Missions
            .FirstOrDefaultAsync(item => item.Id == missionId, cancellationToken);
        if (mission is null)
        {
            return MissionDispatchReissueResult.Failed(missionId, "Mission introuvable.");
        }

        var offers = await db.MissionDispatchOffers
            .Where(offer => offer.MissionId == missionId)
            .OrderBy(offer => offer.Rank)
            .ToListAsync(cancellationToken);

        var expiredCount = 0;
        foreach (var offer in offers.Where(offer => offer.Status == MissionDispatchOfferStatus.Sent && offer.ExpiresAt <= now))
        {
            offer.MarkExpired(now);
            expiredCount++;
        }

        if (offers.Any(offer => offer.Status == MissionDispatchOfferStatus.Accepted))
        {
            await db.SaveChangesAsync(cancellationToken);
            return MissionDispatchReissueResult.Ok(missionId, expiredCount, CreatedOfferCount: 0, "Une entreprise a deja accepte la mission.");
        }

        if (mission.Status is MissionStatus.Completed or MissionStatus.Cancelled or MissionStatus.Disputed or MissionStatus.Resolved)
        {
            await db.SaveChangesAsync(cancellationToken);
            return MissionDispatchReissueResult.Ok(missionId, expiredCount, CreatedOfferCount: 0, "Mission terminee ou non redistribuable.");
        }

        var excludedCompanyIds = offers.Select(offer => offer.CompanyId).ToHashSet();
        var request = new MissionDispatchRequest(
            mission.Id,
            mission.ServiceId,
            mission.ServicePrestationId,
            mission.ServiceAddress,
            mission.Mode == MissionMode.Instant);
        var candidates = await GetCandidatesAsync(mission, excludedCompanyIds, cancellationToken);
        var scores = scoringService.SelectTopCompanies(request, candidates);

        if (scores.Count == 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            return MissionDispatchReissueResult.Ok(missionId, expiredCount, CreatedOfferCount: 0, "Aucune nouvelle entreprise candidate.");
        }

        var expiresAt = now.Add(DefaultCompanyResponseWindow);
        var nextRankStart = offers.Count == 0 ? 1 : offers.Max(offer => offer.Rank) + 1;
        var createdOffers = scores
            .Select((score, index) => new MissionDispatchOffer(
                mission.Id,
                score.CompanyId,
                nextRankStart + index,
                score.Score,
                score.Details,
                expiresAt))
            .ToList();

        foreach (var offer in createdOffers)
        {
            db.MissionDispatchOffers.Add(offer);
        }

        mission.MarkCompanyOffersSent();
        await db.SaveChangesAsync(cancellationToken);

        return MissionDispatchReissueResult.Ok(missionId, expiredCount, createdOffers.Count, "Nouvelle vague envoyee.");
    }

    public async Task<IReadOnlyList<MissionDispatchCandidate>> GetCandidatesAsync(
        Mission mission,
        CancellationToken cancellationToken)
    {
        return await GetCandidatesAsync(mission, excludedCompanyIds: EmptyCompanySet, cancellationToken);
    }

    private async Task<IReadOnlyList<MissionDispatchCandidate>> GetCandidatesAsync(
        Mission mission,
        IReadOnlySet<Guid> excludedCompanyIds,
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
            .Where(company => company.Status == CompanyStatus.Approved && !excludedCompanyIds.Contains(company.Id))
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

        var noResponseCounts = await db.MissionDispatchOffers
            .AsNoTracking()
            .Where(offer => companyIds.Contains(offer.CompanyId) && offer.Status == MissionDispatchOfferStatus.Expired)
            .GroupBy(offer => offer.CompanyId)
            .Select(group => new { CompanyId = group.Key, Count = group.Count() })
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
                    noResponseCounts.FirstOrDefault(item => item.CompanyId == company.Id)?.Count ?? 0,
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

    private static readonly IReadOnlySet<Guid> EmptyCompanySet = new HashSet<Guid>();
}
