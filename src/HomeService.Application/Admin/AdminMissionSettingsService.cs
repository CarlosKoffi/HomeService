using HomeService.Application.Abstractions;
using HomeService.Contracts.Admin;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminMissionSettingsService(IAppDbContext db)
{
    public async Task<AdminMissionSettingsResponse> GetAsync(CancellationToken cancellationToken)
    {
        var rules = await db.CommissionRules
            .AsNoTracking()
            .OrderBy(rule => rule.Target)
            .ThenBy(rule => rule.CompanyId == null ? 0 : 1)
            .ThenBy(rule => rule.ServiceId == null ? 0 : 1)
            .ThenBy(rule => rule.Name)
            .Select(rule => new
            {
                rule.Id,
                rule.Name,
                rule.Target,
                CompanyName = rule.Company == null ? null : rule.Company.Name,
                ServiceName = rule.Service == null ? null : rule.Service.Name,
                PrestationName = rule.ServicePrestation == null ? null : rule.ServicePrestation.Name,
                AssignmentSource = rule.AssignmentSource == null ? null : rule.AssignmentSource.ToString(),
                rule.RateBasisPoints,
                rule.FixedAmount,
                rule.Currency,
                rule.IsActive,
                rule.EffectiveFrom,
                rule.EffectiveUntil
            })
            .ToListAsync(cancellationToken);

        return new AdminMissionSettingsResponse(
            rules
                .Select(rule => new AdminCommissionRuleResponse(
                    rule.Id,
                    rule.Name,
                    rule.Target.ToString(),
                    ToTargetLabel(rule.Target),
                    BuildScopeLabel(rule.CompanyName, rule.ServiceName, rule.PrestationName, rule.AssignmentSource),
                    rule.RateBasisPoints,
                    rule.RateBasisPoints / 100m,
                    rule.FixedAmount,
                    rule.Currency,
                    rule.IsActive,
                    rule.EffectiveFrom,
                    rule.EffectiveUntil))
                .ToList());
    }

    public async Task<AdminMissionSettingsOperationResult> UpdateCommissionRuleAsync(
        Guid ruleId,
        UpdateAdminCommissionRuleRequest request,
        CancellationToken cancellationToken)
    {
        var rule = await db.CommissionRules
            .FirstOrDefaultAsync(item => item.Id == ruleId, cancellationToken);

        if (rule is null)
        {
            return AdminMissionSettingsOperationResult.NotFound("Regle de commission introuvable.");
        }

        if (request.RateBasisPoints is < 0 or > 10000)
        {
            return AdminMissionSettingsOperationResult.ValidationFailed("Le pourcentage doit etre compris entre 0% et 100%.");
        }

        rule.UpdatePricing(request.RateBasisPoints, request.FixedAmount, request.Currency);
        await db.SaveChangesAsync(cancellationToken);

        return AdminMissionSettingsOperationResult.Ok();
    }

    private static string? BuildScopeLabel(string? companyName, string? serviceName, string? prestationName, string? assignmentSource)
    {
        var parts = new[] { companyName, serviceName, prestationName, assignmentSource }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        return parts.Count == 0 ? null : string.Join(" - ", parts);
    }

    private static string ToTargetLabel(CommissionRuleTarget target)
    {
        return target switch
        {
            CommissionRuleTarget.PlatformConnection => "Commission mise en relation",
            CommissionRuleTarget.KazaAssignmentExtra => "Surcommission affectation wélé",
            _ => target.ToString()
        };
    }
}

public sealed record AdminMissionSettingsOperationResult(
    AdminMissionSettingsOperationStatus Status,
    string? Message)
{
    public static AdminMissionSettingsOperationResult Ok()
        => new(AdminMissionSettingsOperationStatus.Ok, null);

    public static AdminMissionSettingsOperationResult NotFound(string message)
        => new(AdminMissionSettingsOperationStatus.NotFound, message);

    public static AdminMissionSettingsOperationResult ValidationFailed(string message)
        => new(AdminMissionSettingsOperationStatus.ValidationFailed, message);
}

public enum AdminMissionSettingsOperationStatus
{
    Ok,
    NotFound,
    ValidationFailed
}
