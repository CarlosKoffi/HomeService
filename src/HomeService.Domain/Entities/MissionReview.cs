using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class MissionReview : AuditableEntity
{
    private MissionReview()
    {
    }

    public MissionReview(
        Guid missionId,
        Guid customerId,
        Guid companyId,
        Guid providerId,
        int qualityRating,
        int punctualityRating,
        int presentationRating,
        int politenessRating,
        int cleanlinessRating,
        string? comment)
    {
        MissionId = missionId;
        CustomerId = customerId;
        CompanyId = companyId;
        ProviderId = providerId;
        QualityRating = NormalizeRating(qualityRating);
        PunctualityRating = NormalizeRating(punctualityRating);
        PresentationRating = NormalizeRating(presentationRating);
        PolitenessRating = NormalizeRating(politenessRating);
        CleanlinessRating = NormalizeRating(cleanlinessRating);
        OverallRating = (int)Math.Round((QualityRating + PunctualityRating + PresentationRating + PolitenessRating + CleanlinessRating) / 5m, MidpointRounding.AwayFromZero);
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        SubmittedAt = DateTimeOffset.UtcNow;
    }

    public Guid MissionId { get; private set; }
    public Mission? Mission { get; private set; }
    public Guid CustomerId { get; private set; }
    public CustomerProfile? Customer { get; private set; }
    public Guid CompanyId { get; private set; }
    public Company? Company { get; private set; }
    public Guid ProviderId { get; private set; }
    public ProviderProfile? Provider { get; private set; }
    public int QualityRating { get; private set; }
    public int PunctualityRating { get; private set; }
    public int PresentationRating { get; private set; }
    public int PolitenessRating { get; private set; }
    public int CleanlinessRating { get; private set; }
    public int OverallRating { get; private set; }
    public string? Comment { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; }

    private static int NormalizeRating(int rating)
    {
        return Math.Clamp(rating, 1, 5);
    }
}
