using HomeService.Application.Missions;
using HomeService.Domain.Enums;

namespace HomeService.Tests.Unit.Application;

public sealed class MissionCustomerContactAccessPolicyTests
{
    [Theory]
    [InlineData(MissionStatus.Created)]
    [InlineData(MissionStatus.SearchingProvider)]
    [InlineData(MissionStatus.Offered)]
    [InlineData(MissionStatus.Accepted)]
    [InlineData(MissionStatus.Assigned)]
    [InlineData(MissionStatus.OnTheWay)]
    [InlineData(MissionStatus.Started)]
    public void CanAccess_WhenMissionCanStillBeWorked_ReturnsTrue(MissionStatus status)
        => Assert.True(MissionCustomerContactAccessPolicy.CanAccess(status));

    [Theory]
    [InlineData(MissionStatus.Completed)]
    [InlineData(MissionStatus.Cancelled)]
    [InlineData(MissionStatus.Disputed)]
    [InlineData(MissionStatus.Resolved)]
    public void CanAccess_WhenMissionIsClosed_ReturnsFalse(MissionStatus status)
        => Assert.False(MissionCustomerContactAccessPolicy.CanAccess(status));
}
