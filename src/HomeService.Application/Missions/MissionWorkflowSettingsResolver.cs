using HomeService.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Missions;

public static class MissionWorkflowSettingsResolver
{
    public const string CompanyOfferResponseMinutes = "company_offer_response_minutes";
    public const string UrgentCompanyOfferResponseMinutes = "urgent_company_offer_response_minutes";
    public const string CompanyProviderAssignmentMinutes = "company_provider_assignment_minutes";
    public const string ProviderAcceptanceMinutes = "provider_acceptance_minutes";
    public const string ScheduledProviderAcceptanceMinutes = "scheduled_provider_acceptance_minutes";
    public const string CustomerQuoteValidityMinutes = "customer_quote_validity_minutes";
    public const string UrgentMissionsEnabled = "urgent_missions_enabled";
    public const string ProviderReeligibilityRounds = "provider_reeligibility_rounds";

    public static async Task<int> ResolveIntAsync(
        IAppDbContext db,
        string key,
        int fallbackValue,
        CancellationToken cancellationToken)
    {
        var value = await db.MissionWorkflowSettings
            .AsNoTracking()
            .Where(setting => setting.Key == key && setting.IsActive)
            .Select(setting => (int?)setting.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return Math.Max(1, value ?? fallbackValue);
    }

    public static async Task<TimeSpan> ResolveMinutesAsync(
        IAppDbContext db,
        string key,
        int fallbackMinutes,
        CancellationToken cancellationToken)
    {
        return TimeSpan.FromMinutes(await ResolveIntAsync(db, key, fallbackMinutes, cancellationToken));
    }

    public static async Task<bool> ResolveFlagAsync(
        IAppDbContext db,
        string key,
        bool fallbackValue,
        CancellationToken cancellationToken)
    {
        var value = await db.MissionWorkflowSettings
            .AsNoTracking()
            .Where(setting => setting.Key == key && setting.IsActive)
            .Select(setting => (int?)setting.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return value.HasValue ? value.Value == 1 : fallbackValue;
    }
}
