namespace HomeService.Application.Missions;

public static class ProviderMissionReeligibilityPolicy
{
    public static int GetBlockingRoundFloor(int currentRound, int resetEveryRounds)
    {
        var round = Math.Max(1, currentRound);
        var cycleLength = Math.Max(1, resetEveryRounds);

        if (round < cycleLength)
        {
            return 1;
        }

        return round - (round % cycleLength);
    }
}
