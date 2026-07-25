using HomeService.Application.Missions;

namespace HomeService.Tests.Unit.Application;

public sealed class MissionDispatchScoringServiceTests
{
    private static readonly Guid MissionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ServiceId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public void SelectTopCompanies_KeepsLowestManualPriorityFirst()
    {
        var service = new MissionDispatchScoringService();
        var request = CreateRequest();
        var highPriorityCompany = CreateCandidate("Prioritaire", manualPriority: 1);
        var lowPriorityCompany = CreateCandidate("Secondaire", manualPriority: 5, averageRating: 5, completedMissionCount: 50);

        var result = service.SelectTopCompanies(request, [lowPriorityCompany, highPriorityCompany]);

        Assert.Equal(highPriorityCompany.CompanyId, result[0].CompanyId);
        Assert.Equal(1, result[0].Rank);
    }

    [Fact]
    public void SelectTopCompanies_ReturnsOnlyRequestedTopCompanies()
    {
        var service = new MissionDispatchScoringService();
        var request = CreateRequest(maxCompanies: 3);
        var candidates = Enumerable.Range(1, 5)
            .Select(index => CreateCandidate($"Entreprise {index}", manualPriority: index))
            .ToList();

        var result = service.SelectTopCompanies(request, candidates);

        Assert.Equal(3, result.Count);
        Assert.Equal([1, 2, 3], result.Select(item => item.Rank));
    }

    [Fact]
    public void Score_WhenMissionIsUrgent_RewardsUrgentCompanies()
    {
        var service = new MissionDispatchScoringService();
        var request = CreateRequest(isUrgent: true);
        var urgent = CreateCandidate("Urgente", acceptsUrgentMissions: true);
        var normal = CreateCandidate("Normale", acceptsUrgentMissions: false);

        var urgentScore = service.Score(request, urgent);
        var normalScore = service.Score(request, normal);

        Assert.True(urgentScore.Score < normalScore.Score);
        Assert.Contains("urgencyAdjustment=-180", urgentScore.Details);
    }

    [Fact]
    public void Score_AddsPenaltiesForRecentLoadCancellationsNoResponseAndPriceDeviation()
    {
        var service = new MissionDispatchScoringService();
        var request = CreateRequest();
        var reliable = CreateCandidate("Fiable");
        var risky = CreateCandidate(
            "Risque",
            recentMissionCount: 4,
            cancellationCount: 2,
            noResponseCount: 3,
            priceDeviationPercent: 20);

        var reliableScore = service.Score(request, reliable);
        var riskyScore = service.Score(request, risky);

        Assert.True(riskyScore.Score > reliableScore.Score);
        Assert.Contains("recentPenalty=140", riskyScore.Details);
        Assert.Contains("cancellationPenalty=240", riskyScore.Details);
        Assert.Contains("noResponsePenalty=240", riskyScore.Details);
        Assert.Contains("pricePenalty=120", riskyScore.Details);
    }

    [Fact]
    public void Score_RewardsGoodReputationAndCompletedExperience()
    {
        var service = new MissionDispatchScoringService();
        var request = CreateRequest();
        var newCompany = CreateCandidate("Nouvelle");
        var experienced = CreateCandidate("Experimentee", averageRating: 5, completedMissionCount: 20);

        var newScore = service.Score(request, newCompany);
        var experiencedScore = service.Score(request, experienced);

        Assert.True(experiencedScore.Score < newScore.Score);
        Assert.Contains("reputationBonus=220", experiencedScore.Details);
        Assert.Contains("experienceBonus=120", experiencedScore.Details);
    }

    private static MissionDispatchRequest CreateRequest(bool isUrgent = false, int maxCompanies = 3)
    {
        return new MissionDispatchRequest(MissionId, ServiceId, null, "Cocody Angre", isUrgent, maxCompanies);
    }

    private static MissionDispatchCandidate CreateCandidate(
        string name,
        int manualPriority = 2,
        bool coversRequestedZone = true,
        bool acceptsUrgentMissions = false,
        decimal? averageRating = null,
        int completedMissionCount = 0,
        int recentMissionCount = 0,
        int cancellationCount = 0,
        int noResponseCount = 0,
        decimal? priceDeviationPercent = null)
    {
        return new MissionDispatchCandidate(
            Guid.NewGuid(),
            name,
            manualPriority,
            coversRequestedZone,
            acceptsUrgentMissions,
            averageRating,
            completedMissionCount,
            recentMissionCount,
            cancellationCount,
            noResponseCount,
            priceDeviationPercent);
    }
}
