using HomeService.Domain.Enums;

namespace HomeService.Application.Missions;

public static class MissionCustomerContactAccessPolicy
{
    public static bool CanAccess(MissionStatus status)
        => status is not MissionStatus.Completed
            and not MissionStatus.Cancelled
            and not MissionStatus.Disputed
            and not MissionStatus.Resolved;
}
