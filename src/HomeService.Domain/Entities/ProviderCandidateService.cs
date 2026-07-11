using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class ProviderCandidateService : AuditableEntity
{
    private ProviderCandidateService()
    {
    }

    public ProviderCandidateService(Guid providerId, Guid serviceId, ExperienceLevel experienceLevel, int yearsOfExperience)
    {
        ProviderId = providerId;
        ServiceId = serviceId;
        ExperienceLevel = experienceLevel;
        YearsOfExperience = Math.Max(0, yearsOfExperience);
    }

    public Guid ProviderId { get; private set; }
    public ProviderProfile? Provider { get; private set; }
    public Guid ServiceId { get; private set; }
    public Service? Service { get; private set; }
    public ExperienceLevel ExperienceLevel { get; private set; } = ExperienceLevel.Confirmed;
    public int YearsOfExperience { get; private set; }
    public bool IsActive { get; private set; } = true;

    public void Update(ExperienceLevel experienceLevel, int yearsOfExperience)
    {
        ExperienceLevel = experienceLevel;
        YearsOfExperience = Math.Max(0, yearsOfExperience);
        IsActive = true;
        Touch();
    }

    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }
}
