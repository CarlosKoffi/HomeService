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
        ProviderServicePriceTier priceTier = ProviderServicePriceTier.Normal)
    {
        ProviderId = providerId;
        CompanyId = companyId;
        ServiceId = serviceId;
        ExperienceLevel = experienceLevel;
        YearsOfExperience = yearsOfExperience;
        PriceTier = priceTier;
        CompanyValidatedAt = DateTimeOffset.UtcNow;
    }

    public Guid ProviderId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid ServiceId { get; private set; }
    public Service? Service { get; private set; }
    public ExperienceLevel ExperienceLevel { get; private set; }
    public int YearsOfExperience { get; private set; }
    public ProviderServicePriceTier PriceTier { get; private set; } = ProviderServicePriceTier.Normal;
    public PricingUnit PricingUnit { get; private set; } = PricingUnit.Hourly;
    public DateTimeOffset CompanyValidatedAt { get; private set; }
    public bool IsActive { get; private set; } = true;

    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }

    public void UpdateCompanyExperience(ExperienceLevel experienceLevel, int yearsOfExperience, ProviderServicePriceTier priceTier)
    {
        ExperienceLevel = experienceLevel;
        YearsOfExperience = yearsOfExperience;
        PriceTier = priceTier;
        CompanyValidatedAt = DateTimeOffset.UtcNow;
        IsActive = true;
        Touch();
    }
}
