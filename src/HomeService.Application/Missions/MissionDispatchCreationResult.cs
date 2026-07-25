using HomeService.Domain.Entities;

namespace HomeService.Application.Missions;

public sealed record MissionDispatchCreationResult(
    bool IsSuccess,
    string? Message,
    IReadOnlyList<MissionDispatchOffer> Offers)
{
    public static MissionDispatchCreationResult Ok(IReadOnlyList<MissionDispatchOffer> offers)
    {
        return new MissionDispatchCreationResult(true, null, offers);
    }

    public static MissionDispatchCreationResult Failed(string message)
    {
        return new MissionDispatchCreationResult(false, message, []);
    }
}
