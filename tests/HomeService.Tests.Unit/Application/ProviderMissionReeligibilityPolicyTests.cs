using HomeService.Application.Missions;

namespace HomeService.Tests.Unit.Application;

public sealed class ProviderMissionReeligibilityPolicyTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    public void Before_configured_round_previous_attempts_still_block(int currentRound, int expectedFloor)
    {
        Assert.Equal(expectedFloor, ProviderMissionReeligibilityPolicy.GetBlockingRoundFloor(currentRound, 4));
    }

    [Theory]
    [InlineData(4, 4)]
    [InlineData(5, 4)]
    [InlineData(7, 4)]
    [InlineData(8, 8)]
    public void At_each_cycle_old_attempts_stop_blocking(int currentRound, int expectedFloor)
    {
        Assert.Equal(expectedFloor, ProviderMissionReeligibilityPolicy.GetBlockingRoundFloor(currentRound, 4));
    }

    [Fact]
    public void Invalid_configuration_is_safely_normalized()
    {
        Assert.Equal(1, ProviderMissionReeligibilityPolicy.GetBlockingRoundFloor(0, 0));
    }
}
