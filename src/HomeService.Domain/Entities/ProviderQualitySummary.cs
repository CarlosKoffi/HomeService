using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class ProviderQualitySummary : AuditableEntity
{
    private ProviderQualitySummary()
    {
    }

    public ProviderQualitySummary(Guid providerId, Guid serviceId, Guid? servicePrestationId)
    {
        ProviderId = providerId;
        ServiceId = serviceId;
        ServicePrestationId = servicePrestationId;
    }

    public Guid ProviderId { get; private set; }
    public ProviderProfile? Provider { get; private set; }
    public Guid ServiceId { get; private set; }
    public Service? Service { get; private set; }
    public Guid? ServicePrestationId { get; private set; }
    public ServicePrestation? ServicePrestation { get; private set; }
    public int Score { get; private set; } = 70;
    public ProviderQualityLevel Level { get; private set; } = ProviderQualityLevel.New;
    public int CompletedMissionCount { get; private set; }
    public int AuditedMissionCount { get; private set; }
    public int PassedAuditCount { get; private set; }
    public int ConfirmedIncidentCount { get; private set; }
    public decimal AverageRating { get; private set; }
    public decimal PunctualityRate { get; private set; }
    public DateTimeOffset CalculatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public void Recalculate(
        int score,
        ProviderQualityLevel level,
        int completedMissionCount,
        int auditedMissionCount,
        int passedAuditCount,
        int confirmedIncidentCount,
        decimal averageRating,
        decimal punctualityRate)
    {
        Score = Math.Clamp(score, 0, 100);
        Level = level;
        CompletedMissionCount = Math.Max(0, completedMissionCount);
        AuditedMissionCount = Math.Max(0, auditedMissionCount);
        PassedAuditCount = Math.Clamp(passedAuditCount, 0, AuditedMissionCount);
        ConfirmedIncidentCount = Math.Max(0, confirmedIncidentCount);
        AverageRating = Math.Clamp(averageRating, 0m, 5m);
        PunctualityRate = Math.Clamp(punctualityRate, 0m, 100m);
        CalculatedAt = DateTimeOffset.UtcNow;
        Touch();
    }
}
