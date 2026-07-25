namespace HomeService.Application.Missions;

public sealed record MissionDispatchReissueResult(
    Guid MissionId,
    bool IsSuccess,
    int ExpiredOfferCount,
    int CreatedOfferCount,
    string Message)
{
    public static MissionDispatchReissueResult Ok(
        Guid missionId,
        int ExpiredOfferCount,
        int CreatedOfferCount,
        string message)
    {
        return new MissionDispatchReissueResult(missionId, true, ExpiredOfferCount, CreatedOfferCount, message);
    }

    public static MissionDispatchReissueResult Failed(Guid missionId, string message)
    {
        return new MissionDispatchReissueResult(missionId, false, 0, 0, message);
    }
}

public sealed record MissionDispatchReissueBatchResult(IReadOnlyList<MissionDispatchReissueResult> Items)
{
    public int MissionCount => Items.Count;
    public int ExpiredOfferCount => Items.Sum(item => item.ExpiredOfferCount);
    public int CreatedOfferCount => Items.Sum(item => item.CreatedOfferCount);
}
