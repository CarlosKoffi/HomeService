using HomeService.Domain.Enums;

namespace HomeService.Application.Missions;

public static class MissionConversationAccessPolicy
{
    public static bool CanAccess(MissionStatus status)
        => status is MissionStatus.Accepted
            or MissionStatus.OnTheWay
            or MissionStatus.Started;
}
