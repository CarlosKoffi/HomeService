using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class ProviderService : AuditableEntity
{
    private readonly List<ProviderServicePrestation> _prestations = [];

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
    public IReadOnlyCollection<ProviderServicePrestation> Prestations => _prestations;

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

    public void SyncPrestations(IEnumerable<Guid> servicePrestationIds)
    {
        var requestedIds = servicePrestationIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToHashSet();

        foreach (var existing in _prestations.Where(prestation => prestation.IsActive && !requestedIds.Contains(prestation.ServicePrestationId)))
        {
            existing.Deactivate();
        }

        foreach (var requestedId in requestedIds)
        {
            var existing = _prestations.FirstOrDefault(prestation => prestation.ServicePrestationId == requestedId);
            if (existing is null)
            {
                _prestations.Add(new ProviderServicePrestation(Id, requestedId));
            }
            else
            {
                existing.Activate();
            }
        }

        Touch();
    }
}
