using HomeService.Application.Abstractions;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Missions;

public sealed class MissionCommercialPricingService(IAppDbContext db)
{
    public const int DefaultCompanyCommissionRateBasisPoints = 1500;
    public const int DefaultCustomerServiceFeeRateBasisPoints = 750;
    public const int DefaultMinimumRatingHundredths = 450;
    public const int DefaultMinimumRatingCount = 10;
    public const int DefaultMaximumCompanyCancellationRateBasisPoints = 500;
    public const int DefaultCancellationLookbackMissionCount = 100;

    public async Task<MissionCommercialPricing> CalculateAsync(
        Mission mission,
        int quotedAmount,
        CancellationToken cancellationToken)
    {
        var normalizedQuote = Math.Max(0, quotedAmount);
        var partsAmount = Math.Clamp(mission.PartsEstimateAmount.GetValueOrDefault(), 0, normalizedQuote);
        var commissionableAmount = Math.Max(0, normalizedQuote - partsAmount);

        if (mission.CustomerConfirmedAt is not null && mission.CustomerTotalAmount > 0)
        {
            return new MissionCommercialPricing(
                normalizedQuote,
                partsAmount,
                mission.CommissionableAmount,
                mission.PlatformCommissionRateBasisPoints,
                mission.PlatformCommissionAmount,
                mission.CustomerServiceFeeRateBasisPoints,
                mission.CustomerServiceFeeAmount,
                mission.CustomerTotalAmount,
                mission.CompanyPayoutAmount,
                mission.IsFirstCustomerCompanyOrder,
                mission.Currency,
                mission.CompanyCommissionTierName ?? "Historique",
                mission.CompanyCommissionMissionSequence);
        }

        var isFirstCustomerCompanyOrder = await IsFirstCustomerCompanyOrderAsync(mission, cancellationToken);
        var companyTier = await ResolveCompanyTierAsync(mission, cancellationToken);
        var customerFeeRule = await ResolveRuleAsync(
            mission,
            CommissionRuleTarget.CustomerServiceFee,
            DefaultCustomerServiceFeeRateBasisPoints,
            allowLegacyPlatformRule: false,
            cancellationToken);

        var companyCommissionAmount = CalculatePercentage(commissionableAmount, companyTier.RateBasisPoints);
        var customerServiceFeeAmount = customerFeeRule.CalculateAmount(commissionableAmount);

        return new MissionCommercialPricing(
            normalizedQuote,
            partsAmount,
            commissionableAmount,
            companyTier.RateBasisPoints,
            companyCommissionAmount,
            customerFeeRule.RateBasisPoints,
            customerServiceFeeAmount,
            normalizedQuote + customerServiceFeeAmount,
            Math.Max(0, normalizedQuote - companyCommissionAmount),
            isFirstCustomerCompanyOrder,
            mission.Currency,
            companyTier.Name,
            companyTier.MissionSequence);
    }

    public async Task<CompanyCommissionProgress> GetCompanyCommissionProgressAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var company = await db.Companies.FirstOrDefaultAsync(item => item.Id == companyId, cancellationToken);
        if (company is null)
        {
            return CompanyCommissionProgress.Empty;
        }

        var tiers = await LoadCompanyTiersAsync(cancellationToken);
        var completedMissionCount = await GetCompletedMissionCountAsync(companyId, cancellationToken);
        var metrics = await GetQualityMetricsAsync(companyId, cancellationToken);
        var currentTier = ResolveCurrentTier(company, tiers);
        var nextTier = tiers.FirstOrDefault(item => item.MinimumMissionCount > currentTier.MinimumMissionCount);

        return new CompanyCommissionProgress(
            currentTier.Name,
            currentTier.RateBasisPoints,
            completedMissionCount,
            nextTier?.MinimumMissionCount,
            nextTier?.Name,
            Math.Max(0, (nextTier?.MinimumMissionCount ?? completedMissionCount) - completedMissionCount),
            metrics.RatingCount,
            metrics.AverageRating,
            metrics.CompanyCancellationRateBasisPoints,
            metrics.DocumentsCompliant,
            metrics.HasOpenDispute,
            metrics.IsEligible);
    }

    private async Task<ResolvedCompanyCommissionTier> ResolveCompanyTierAsync(
        Mission mission,
        CancellationToken cancellationToken)
    {
        if (mission.CompanyId is null)
        {
            return new ResolvedCompanyCommissionTier("Lancement", DefaultCompanyCommissionRateBasisPoints, 1);
        }

        var company = await db.Companies.FirstAsync(item => item.Id == mission.CompanyId.Value, cancellationToken);
        var tiers = await LoadCompanyTiersAsync(cancellationToken);
        var completedMissionCount = await GetCompletedMissionCountAsync(company.Id, cancellationToken);
        var missionSequence = completedMissionCount + 1;
        var currentTier = ResolveCurrentTier(company, tiers);
        var targetTier = tiers
            .Where(item => item.MinimumMissionCount <= missionSequence)
            .OrderByDescending(item => item.MinimumMissionCount)
            .FirstOrDefault() ?? tiers[0];

        if (targetTier.MinimumMissionCount > currentTier.MinimumMissionCount)
        {
            var metrics = await GetQualityMetricsAsync(company.Id, cancellationToken);
            if (metrics.IsEligible)
            {
                currentTier = targetTier;
            }
        }

        company.PromoteCommissionTier(currentTier.Name, currentTier.MinimumMissionCount, currentTier.RateBasisPoints);
        return new ResolvedCompanyCommissionTier(currentTier.Name, currentTier.RateBasisPoints, missionSequence);
    }

    private async Task<List<CompanyCommissionTier>> LoadCompanyTiersAsync(CancellationToken cancellationToken)
    {
        var tiers = await db.CompanyCommissionTiers
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.MinimumMissionCount)
            .ThenBy(item => item.SortOrder)
            .ToListAsync(cancellationToken);

        return tiers.Count > 0
            ? tiers
            : [new CompanyCommissionTier("Lancement", 1, DefaultCompanyCommissionRateBasisPoints, 10)];
    }

    private static CompanyCommissionTier ResolveCurrentTier(Company company, IReadOnlyList<CompanyCommissionTier> tiers)
    {
        return tiers.FirstOrDefault(item => string.Equals(item.Name, company.CurrentCommissionTierName, StringComparison.OrdinalIgnoreCase))
            ?? tiers.FirstOrDefault(item => item.MinimumMissionCount == company.CurrentCommissionTierMinimumMissionCount)
            ?? tiers
            .Where(item => item.MinimumMissionCount <= Math.Max(1, company.CurrentCommissionTierMinimumMissionCount))
            .OrderByDescending(item => item.MinimumMissionCount)
            .FirstOrDefault() ?? tiers[0];
    }

    private Task<int> GetCompletedMissionCountAsync(Guid companyId, CancellationToken cancellationToken)
    {
        return db.Missions.AsNoTracking().CountAsync(item => item.CompanyId == companyId
            && item.Status == MissionStatus.Completed
            && item.PaymentStatus == PaymentStatus.Paid,
            cancellationToken);
    }

    private async Task<CompanyCommissionQualityMetrics> GetQualityMetricsAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var settings = await db.MissionWorkflowSettings.AsNoTracking()
            .Where(item => item.IsActive && new[]
            {
                "company_commission_minimum_rating_hundredths",
                "company_commission_minimum_rating_count",
                "company_commission_maximum_cancellation_basis_points",
                "company_commission_cancellation_lookback"
            }.Contains(item.Key))
            .ToDictionaryAsync(item => item.Key, item => item.Value, cancellationToken);

        var minimumRating = settings.GetValueOrDefault("company_commission_minimum_rating_hundredths", DefaultMinimumRatingHundredths) / 100m;
        var minimumRatingCount = settings.GetValueOrDefault("company_commission_minimum_rating_count", DefaultMinimumRatingCount);
        var maximumCancellationRate = settings.GetValueOrDefault("company_commission_maximum_cancellation_basis_points", DefaultMaximumCompanyCancellationRateBasisPoints);
        var cancellationLookback = settings.GetValueOrDefault("company_commission_cancellation_lookback", DefaultCancellationLookbackMissionCount);

        var ratings = await db.MissionReviews.AsNoTracking()
            .Where(item => item.CompanyId == companyId)
            .OrderByDescending(item => item.SubmittedAt)
            .Select(item => item.OverallRating)
            .Take(50)
            .ToListAsync(cancellationToken);

        var recentOutcomes = await db.Missions.AsNoTracking()
            .Where(item => item.CompanyId == companyId
                && (item.Status == MissionStatus.Completed || item.Status == MissionStatus.Cancelled))
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new { item.Status, item.CancelledBy })
            .Take(Math.Max(1, cancellationLookback))
            .ToListAsync(cancellationToken);
        var companyCancellationCount = recentOutcomes.Count(item => item.Status == MissionStatus.Cancelled
            && item.CancelledBy is MissionCancellationActor.Company or MissionCancellationActor.Provider);
        var cancellationRate = recentOutcomes.Count == 0
            ? 0
            : (int)Math.Round(companyCancellationCount * 10000m / recentOutcomes.Count, MidpointRounding.AwayFromZero);

        var documentsCompliant = await db.Companies.AsNoTracking()
            .AnyAsync(item => item.Id == companyId && item.Status == CompanyStatus.Approved, cancellationToken);
        var hasNonCompliantApplicationDocument = await db.CompanyApplicationDocuments.AsNoTracking()
            .AnyAsync(document => document.CompanyApplication != null
                && document.CompanyApplication.CompanyId == companyId
                && document.ReviewStatus != DocumentReviewStatus.Approved,
                cancellationToken);
        documentsCompliant &= !hasNonCompliantApplicationDocument;

        var hasOpenDispute = await db.MissionDisputes.AsNoTracking()
            .AnyAsync(dispute => dispute.Mission != null
                && dispute.Mission.CompanyId == companyId
                && dispute.Status == MissionDisputeStatus.Open,
                cancellationToken);
        var averageRating = ratings.Count == 0 ? 0m : ratings.Sum() / (decimal)ratings.Count;
        var eligible = ratings.Count >= minimumRatingCount
            && averageRating >= minimumRating
            && cancellationRate <= maximumCancellationRate
            && documentsCompliant
            && !hasOpenDispute;

        return new CompanyCommissionQualityMetrics(
            ratings.Count,
            averageRating,
            cancellationRate,
            documentsCompliant,
            hasOpenDispute,
            eligible);
    }

    private static int CalculatePercentage(int amount, int rateBasisPoints)
    {
        return (int)Math.Round(Math.Max(0, amount) * Math.Clamp(rateBasisPoints, 0, 10000) / 10000m, MidpointRounding.AwayFromZero);
    }

    private async Task<bool> IsFirstCustomerCompanyOrderAsync(
        Mission mission,
        CancellationToken cancellationToken)
    {
        if (mission.CompanyId is null)
        {
            return true;
        }

        return !await db.Missions
            .AsNoTracking()
            .AnyAsync(item => item.Id != mission.Id
                && item.CustomerId == mission.CustomerId
                && item.CompanyId == mission.CompanyId
                && item.CustomerConfirmedAt != null,
                cancellationToken);
    }

    private async Task<CommissionRule> ResolveRuleAsync(
        Mission mission,
        CommissionRuleTarget target,
        int defaultRateBasisPoints,
        bool allowLegacyPlatformRule,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var targets = allowLegacyPlatformRule
            ? new[] { target, CommissionRuleTarget.PlatformConnection }
            : new[] { target };
        var rules = await db.CommissionRules
            .AsNoTracking()
            .Where(rule => rule.IsActive
                && targets.Contains(rule.Target)
                && rule.EffectiveFrom <= now
                && (rule.EffectiveUntil == null || rule.EffectiveUntil > now)
                && (rule.CompanyId == null || rule.CompanyId == mission.CompanyId)
                && (rule.ServiceId == null || rule.ServiceId == mission.ServiceId)
                && (rule.ServicePrestationId == null || rule.ServicePrestationId == mission.ServicePrestationId)
                && (rule.AssignmentSource == null || rule.AssignmentSource == mission.AssignmentSource))
            .ToListAsync(cancellationToken);

        var selected = rules
            .OrderByDescending(rule => rule.Target == target)
            .ThenByDescending(rule => rule.CompanyId.HasValue)
            .ThenByDescending(rule => rule.ServicePrestationId.HasValue)
            .ThenByDescending(rule => rule.ServiceId.HasValue)
            .ThenByDescending(rule => rule.AssignmentSource.HasValue)
            .ThenByDescending(rule => rule.EffectiveFrom)
            .FirstOrDefault();

        return selected ?? new CommissionRule(
            target.ToString(),
            target,
            defaultRateBasisPoints,
            0,
            mission.Currency);
    }
}

public sealed record MissionCommercialPricing(
    int QuotedAmount,
    int PartsAmount,
    int CommissionableAmount,
    int CompanyCommissionRateBasisPoints,
    int CompanyCommissionAmount,
    int CustomerServiceFeeRateBasisPoints,
    int CustomerServiceFeeAmount,
    int CustomerTotalAmount,
    int CompanyPayoutAmount,
    bool IsFirstCustomerCompanyOrder,
    string Currency,
    string CompanyCommissionTierName = "Lancement",
    int CompanyCommissionMissionSequence = 0)
{
    public int ServiceAmount => Math.Max(0, QuotedAmount - PartsAmount);
}

public sealed record CompanyCommissionProgress(
    string CurrentTierName,
    int CurrentRateBasisPoints,
    int CompletedMissionCount,
    int? NextTierMinimumMissionCount,
    string? NextTierName,
    int MissionsUntilNextTier,
    int RatingCount,
    decimal AverageRating,
    int CompanyCancellationRateBasisPoints,
    bool DocumentsCompliant,
    bool HasOpenDispute,
    bool IsQualityEligible)
{
    public static CompanyCommissionProgress Empty { get; } = new(
        "Lancement", MissionCommercialPricingService.DefaultCompanyCommissionRateBasisPoints, 0, 50, null, 50, 0, 0m, 0, false, false, false);
}

internal sealed record ResolvedCompanyCommissionTier(string Name, int RateBasisPoints, int MissionSequence);

internal sealed record CompanyCommissionQualityMetrics(
    int RatingCount,
    decimal AverageRating,
    int CompanyCancellationRateBasisPoints,
    bool DocumentsCompliant,
    bool HasOpenDispute,
    bool IsEligible);
