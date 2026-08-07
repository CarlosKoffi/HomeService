using HomeService.Domain.Entities;

namespace HomeService.Application.Missions;

public sealed record MissionDispatchCreationResult(
    bool IsSuccess,
    string? Message,
    IReadOnlyList<MissionDispatchOffer> Offers,
    bool PreferredCompanyUnavailable)
{
    public static MissionDispatchCreationResult Ok(
        IReadOnlyList<MissionDispatchOffer> offers,
        bool preferredCompanyUnavailable = false)
    {
        return new MissionDispatchCreationResult(true, null, offers, preferredCompanyUnavailable);
    }

    public static MissionDispatchCreationResult Failed(string message)
    {
        return new MissionDispatchCreationResult(false, message, [], false);
    }
}
