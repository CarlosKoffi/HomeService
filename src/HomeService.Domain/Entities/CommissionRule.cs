using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class CommissionRule : AuditableEntity
{
    private CommissionRule()
    {
    }

    public CommissionRule(
        string name,
        CommissionRuleTarget target,
        int rateBasisPoints,
        int fixedAmount,
        string currency,
        Guid? serviceId = null,
        Guid? servicePrestationId = null,
        Guid? companyId = null,
        MissionAssignmentSource? assignmentSource = null)
    {
        Name = name.Trim();
        Target = target;
        ServiceId = serviceId;
        ServicePrestationId = servicePrestationId;
        CompanyId = companyId;
        AssignmentSource = assignmentSource;
        RateBasisPoints = Math.Clamp(rateBasisPoints, 0, 10000);
        FixedAmount = Math.Max(0, fixedAmount);
        Currency = string.IsNullOrWhiteSpace(currency) ? "XOF" : currency.Trim().ToUpperInvariant();
    }

    public string Name { get; private set; } = string.Empty;
    public CommissionRuleTarget Target { get; private set; }
    public Guid? ServiceId { get; private set; }
    public Service? Service { get; private set; }
    public Guid? ServicePrestationId { get; private set; }
    public ServicePrestation? ServicePrestation { get; private set; }
    public Guid? CompanyId { get; private set; }
    public Company? Company { get; private set; }
    public MissionAssignmentSource? AssignmentSource { get; private set; }
    public int RateBasisPoints { get; private set; }
    public int FixedAmount { get; private set; }
    public string Currency { get; private set; } = "XOF";
    public DateTimeOffset EffectiveFrom { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EffectiveUntil { get; private set; }
    public bool IsActive { get; private set; } = true;

    public int CalculateAmount(int baseAmount)
    {
        var percentageAmount = (int)Math.Round(Math.Max(0, baseAmount) * RateBasisPoints / 10000m, MidpointRounding.AwayFromZero);
        return percentageAmount + FixedAmount;
    }

    public void UpdatePricing(int rateBasisPoints, int fixedAmount, string currency)
    {
        RateBasisPoints = Math.Clamp(rateBasisPoints, 0, 10000);
        FixedAmount = Math.Max(0, fixedAmount);
        Currency = string.IsNullOrWhiteSpace(currency) ? "XOF" : currency.Trim().ToUpperInvariant();
        Touch();
    }

    public void End(DateTimeOffset effectiveUntil)
    {
        EffectiveUntil = effectiveUntil;
        IsActive = false;
        Touch();
    }
}
