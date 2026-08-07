using HomeService.Application.Abstractions;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Missions;

public sealed class MissionCommercialPricingService(IAppDbContext db)
{
    public const int DefaultFirstOrderCommissionRateBasisPoints = 1200;
    public const int DefaultRepeatOrderCommissionRateBasisPoints = 900;
    public const int DefaultCustomerServiceFeeRateBasisPoints = 400;

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
                mission.Currency);
        }

        var isFirstCustomerCompanyOrder = await IsFirstCustomerCompanyOrderAsync(mission, cancellationToken);
        var companyTarget = isFirstCustomerCompanyOrder
            ? CommissionRuleTarget.CompanyFirstCustomerOrder
            : CommissionRuleTarget.CompanyRepeatCustomerOrder;
        var companyDefaultRate = isFirstCustomerCompanyOrder
            ? DefaultFirstOrderCommissionRateBasisPoints
            : DefaultRepeatOrderCommissionRateBasisPoints;

        var companyRule = await ResolveRuleAsync(
            mission,
            companyTarget,
            companyDefaultRate,
            allowLegacyPlatformRule: true,
            cancellationToken);
        var customerFeeRule = await ResolveRuleAsync(
            mission,
            CommissionRuleTarget.CustomerServiceFee,
            DefaultCustomerServiceFeeRateBasisPoints,
            allowLegacyPlatformRule: false,
            cancellationToken);

        var companyCommissionAmount = companyRule.CalculateAmount(commissionableAmount);
        var customerServiceFeeAmount = customerFeeRule.CalculateAmount(commissionableAmount);

        return new MissionCommercialPricing(
            normalizedQuote,
            partsAmount,
            commissionableAmount,
            companyRule.RateBasisPoints,
            companyCommissionAmount,
            customerFeeRule.RateBasisPoints,
            customerServiceFeeAmount,
            normalizedQuote + customerServiceFeeAmount,
            Math.Max(0, normalizedQuote - companyCommissionAmount),
            isFirstCustomerCompanyOrder,
            mission.Currency);
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
    string Currency)
{
    public int ServiceAmount => Math.Max(0, QuotedAmount - PartsAmount);
}
