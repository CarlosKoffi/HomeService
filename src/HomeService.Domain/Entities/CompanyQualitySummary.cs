using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class CompanyQualitySummary : AuditableEntity
{
    private CompanyQualitySummary()
    {
    }

    public CompanyQualitySummary(Guid companyId, Guid serviceId, Guid? servicePrestationId)
    {
        CompanyId = companyId;
        ServiceId = serviceId;
        ServicePrestationId = servicePrestationId;
    }

    public Guid CompanyId { get; private set; }
    public Company? Company { get; private set; }
    public Guid ServiceId { get; private set; }
    public Service? Service { get; private set; }
    public Guid? ServicePrestationId { get; private set; }
    public ServicePrestation? ServicePrestation { get; private set; }
    public int Score { get; private set; } = 70;
    public int CompletedMissionCount { get; private set; }
    public int EligibleProviderCount { get; private set; }
    public decimal AverageRating { get; private set; }
    public decimal AuditPassRate { get; private set; }
    public DateTimeOffset CalculatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public void Recalculate(
        int score,
        int completedMissionCount,
        int eligibleProviderCount,
        decimal averageRating,
        decimal auditPassRate)
    {
        Score = Math.Clamp(score, 0, 100);
        CompletedMissionCount = Math.Max(0, completedMissionCount);
        EligibleProviderCount = Math.Max(0, eligibleProviderCount);
        AverageRating = Math.Clamp(averageRating, 0m, 5m);
        AuditPassRate = Math.Clamp(auditPassRate, 0m, 100m);
        CalculatedAt = DateTimeOffset.UtcNow;
        Touch();
    }
}
