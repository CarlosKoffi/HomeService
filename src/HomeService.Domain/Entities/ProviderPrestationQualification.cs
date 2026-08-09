using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class ProviderPrestationQualification : AuditableEntity
{
    private ProviderPrestationQualification()
    {
    }

    public ProviderPrestationQualification(Guid providerId, Guid servicePrestationId)
    {
        ProviderId = providerId;
        ServicePrestationId = servicePrestationId;
    }

    public Guid ProviderId { get; private set; }
    public ProviderProfile? Provider { get; private set; }
    public Guid ServicePrestationId { get; private set; }
    public ServicePrestation? ServicePrestation { get; private set; }
    public ProviderQualificationStatus Status { get; private set; } = ProviderQualificationStatus.PendingReview;
    public int? TheoryScore { get; private set; }
    public int? PracticalScore { get; private set; }
    public string? ReviewNote { get; private set; }
    public Guid? ReviewedByAdminUserId { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }

    public bool IsEligible(DateTimeOffset now) =>
        Status == ProviderQualificationStatus.Approved
        && (ExpiresAt is null || ExpiresAt > now);

    public void Review(
        ProviderQualificationStatus status,
        int? theoryScore,
        int? practicalScore,
        string? note,
        Guid? reviewedByAdminUserId,
        DateTimeOffset? expiresAt)
    {
        Status = status;
        TheoryScore = NormalizeScore(theoryScore);
        PracticalScore = NormalizeScore(practicalScore);
        ReviewNote = Clean(note, 1200);
        ReviewedByAdminUserId = reviewedByAdminUserId;
        ReviewedAt = DateTimeOffset.UtcNow;
        ExpiresAt = expiresAt;
        Touch();
    }

    private static int? NormalizeScore(int? value) => value is null ? null : Math.Clamp(value.Value, 0, 100);

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }
}
