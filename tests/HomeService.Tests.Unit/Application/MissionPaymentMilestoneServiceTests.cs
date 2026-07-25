using HomeService.Application.Missions;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class MissionPaymentMilestoneServiceTests
{
    private static readonly Guid CustomerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ServiceId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ProviderId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid CompanyId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public async Task EnsureMissionMilestonesAsync_WhenCalledTwice_CreatesOneMilestonePerTrigger()
    {
        await using var db = CreateDbContext();
        var mission = CreateCompletedMission();
        db.Missions.Add(mission);
        await db.SaveChangesAsync();
        var sut = new MissionPaymentMilestoneService(db);

        await sut.EnsureMissionStartedMilestoneAsync(mission, CancellationToken.None);
        await sut.EnsureMissionCompletedMilestoneAsync(mission, CancellationToken.None);
        await db.SaveChangesAsync();

        await sut.EnsureMissionStartedMilestoneAsync(mission, CancellationToken.None);
        await sut.EnsureMissionCompletedMilestoneAsync(mission, CancellationToken.None);
        await db.SaveChangesAsync();

        var milestones = await db.MissionPaymentMilestones
            .OrderBy(item => item.SortOrder)
            .ToListAsync();
        Assert.Equal(2, milestones.Count);
        Assert.Equal(MissionPaymentMilestoneTrigger.MissionStarted, milestones[0].Trigger);
        Assert.Equal(0, milestones[0].Amount);
        Assert.Equal(MissionPaymentMilestoneTrigger.MissionCompleted, milestones[1].Trigger);
        Assert.Equal(17_000, milestones[1].Amount);
        Assert.All(milestones, milestone => Assert.Equal(MissionPaymentMilestoneStatus.Pending, milestone.Status));
        Assert.All(milestones, milestone => Assert.NotNull(milestone.DueAt));
    }

    private static Mission CreateCompletedMission()
    {
        var mission = new Mission(CustomerId, ServiceId, MissionMode.Instant, PaymentMethod.MobileMoney, null, 90);
        mission.SetServiceLocation("Cocody", 5.348850m, -4.003150m);
        mission.AssignWithCompanyQuote(ProviderId, CompanyId, 20_000, 25_000, null);
        mission.MarkProviderAccepted(ProviderId, CompanyId);
        mission.ConfirmByCustomer(platformCommissionAmount: 3_000, transportFeeAmount: 0, platformCommissionRateBasisPoints: 1500);
        mission.Start(ProviderId, CompanyId);
        mission.Complete(90);
        return mission;
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }
}
