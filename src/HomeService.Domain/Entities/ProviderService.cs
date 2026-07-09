using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class ProviderService : AuditableEntity
{
    private ProviderService()
    {
    }

    public ProviderService(
        Guid providerId,
        Guid companyId,
        Guid serviceId,
        ExperienceLevel experienceLevel,
        int yearsOfExperience,
        int hourlyRateAmount,
        string currency)
    {
        ProviderId = providerId;
        CompanyId = companyId;
        ServiceId = serviceId;
        ExperienceLevel = experienceLevel;
        YearsOfExperience = yearsOfExperience;
        HourlyRateAmount = hourlyRateAmount;
        Currency = currency.Trim().ToUpperInvariant();
        CompanyValidatedAt = DateTimeOffset.UtcNow;
    }

    public Guid ProviderId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid ServiceId { get; private set; }
    public ExperienceLevel ExperienceLevel { get; private set; }
    public int YearsOfExperience { get; private set; }
    public int HourlyRateAmount { get; private set; }
    public string Currency { get; private set; } = "XOF";
    public PricingUnit PricingUnit { get; private set; } = PricingUnit.Hourly;
    public DateTimeOffset CompanyValidatedAt { get; private set; }
    public bool IsActive { get; private set; } = true;
}
