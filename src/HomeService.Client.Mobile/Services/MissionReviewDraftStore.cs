namespace HomeService.Client.Mobile.Services;

public sealed class MissionReviewDraftStore
{
    public MissionReviewDraft? Current { get; private set; }

    public MissionReviewDraft Begin(Guid missionId)
    {
        if (Current?.MissionId != missionId)
        {
            Current = new MissionReviewDraft(missionId);
        }

        return Current;
    }

    public void Clear(Guid missionId)
    {
        if (Current?.MissionId == missionId)
        {
            Current = null;
        }
    }
}

public sealed class MissionReviewDraft(Guid missionId)
{
    public Guid MissionId { get; } = missionId;
    public int QualityRating { get; set; }
    public int PunctualityRating { get; set; }
    public int PresentationRating { get; set; }
    public int PolitenessRating { get; set; }
    public int CleanlinessRating { get; set; }
    public string? Comment { get; set; }

    public bool HasAllRatings =>
        QualityRating is >= 1 and <= 5
        && PunctualityRating is >= 1 and <= 5
        && PresentationRating is >= 1 and <= 5
        && PolitenessRating is >= 1 and <= 5
        && CleanlinessRating is >= 1 and <= 5;
}
