using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class MissionQualityAudit : AuditableEntity
{
    private MissionQualityAudit()
    {
    }

    public MissionQualityAudit(
        Guid missionId,
        Guid providerId,
        Guid companyId,
        Guid serviceId,
        Guid? servicePrestationId,
        string samplingReason)
    {
        MissionId = missionId;
        ProviderId = providerId;
        CompanyId = companyId;
        ServiceId = serviceId;
        ServicePrestationId = servicePrestationId;
        SamplingReason = CleanRequired(samplingReason, 240);
    }

    public Guid MissionId { get; private set; }
    public Mission? Mission { get; private set; }
    public Guid ProviderId { get; private set; }
    public ProviderProfile? Provider { get; private set; }
    public Guid CompanyId { get; private set; }
    public Company? Company { get; private set; }
    public Guid ServiceId { get; private set; }
    public Service? Service { get; private set; }
    public Guid? ServicePrestationId { get; private set; }
    public ServicePrestation? ServicePrestation { get; private set; }
    public QualityAuditStatus Status { get; private set; } = QualityAuditStatus.Pending;
    public string SamplingReason { get; private set; } = string.Empty;
    public Guid? ReviewedByAdminUserId { get; private set; }
    public int? Score { get; private set; }
    public string? ReviewNote { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }

    public void StartReview(Guid? adminUserId)
    {
        Status = QualityAuditStatus.InReview;
        ReviewedByAdminUserId = adminUserId;
        Touch();
    }

    public void Decide(bool passed, int score, string? note, Guid? adminUserId)
    {
        Status = passed ? QualityAuditStatus.Passed : QualityAuditStatus.Failed;
        Score = Math.Clamp(score, 0, 100);
        ReviewNote = Clean(note, 2000);
        ReviewedByAdminUserId = adminUserId;
        ReviewedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    private static string CleanRequired(string value, int maxLength)
    {
        var cleaned = value?.Trim() ?? string.Empty;
        if (cleaned.Length == 0) throw new ArgumentException("Une valeur est obligatoire.", nameof(value));
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }
}
