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

    public static async Task<TimeSpan> ResolveMinutesAsync(
        IAppDbContext db,
        string key,
        int fallbackMinutes,
        CancellationToken cancellationToken)
    {
        var value = await db.MissionWorkflowSettings
            .AsNoTracking()
            .Where(setting => setting.Key == key && setting.IsActive)
            .Select(setting => (int?)setting.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return TimeSpan.FromMinutes(Math.Max(1, value ?? fallbackMinutes));
    }
}
