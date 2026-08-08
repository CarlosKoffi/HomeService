using HomeService.Application.Missions;
using HomeService.Domain.Enums;

namespace HomeService.Tests.Unit.Application;

public sealed class MissionConversationAccessPolicyTests
{
    [Theory]
    [InlineData(MissionStatus.Accepted)]
    [InlineData(MissionStatus.OnTheWay)]
    [InlineData(MissionStatus.Started)]
    public void CanAccess_WhenMissionIsActive_ReturnsTrue(MissionStatus status)
        => Assert.True(MissionConversationAccessPolicy.CanAccess(status));

    [Theory]
    [InlineData(MissionStatus.Created)]
    [InlineData(MissionStatus.SearchingProvider)]
    [InlineData(MissionStatus.Offered)]
    [InlineData(MissionStatus.Assigned)]
    [InlineData(MissionStatus.Completed)]
    [InlineData(MissionStatus.Cancelled)]
    [InlineData(MissionStatus.Disputed)]
    [InlineData(MissionStatus.Resolved)]
    public void CanAccess_WhenMissionIsNotActive_ReturnsFalse(MissionStatus status)
        => Assert.False(MissionConversationAccessPolicy.CanAccess(status));
}
