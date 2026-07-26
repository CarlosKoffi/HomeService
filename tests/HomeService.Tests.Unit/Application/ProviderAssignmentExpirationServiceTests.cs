using HomeService.Application.Missions;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class ProviderAssignmentExpirationServiceTests
{
    [Fact]
    public async Task ExpireDueAssignmentsAsync_WhenAssignmentIsPastDeadline_MarksItExpiredAndReleasesMission()
    {
        await using var db = CreateDbContext();
        var providerId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var mission = new Mission(
            Guid.NewGuid(),
            Guid.NewGuid(),
            MissionMode.Instant,
            PaymentMethod.MobileMoney,
            null,
            90);
        mission.Assign(providerId, companyId, hourlyRateAmount: 5000);
        var assignment = new ProviderMissionAssignment(
            mission.Id,
            providerId,
            companyId,
            DateTimeOffset.UtcNow.AddMinutes(-1));

        db.Missions.Add(mission);
        db.ProviderMissionAssignments.Add(assignment);
        await db.SaveChangesAsync();

        var result = await new ProviderAssignmentExpirationService(db).ExpireDueAssignmentsAsync(
            DateTimeOffset.UtcNow,
            batchSize: 10,
            CancellationToken.None);

        Assert.Equal(1, result.ExpiredAssignmentCount);
        Assert.Equal(ProviderMissionAssignmentStatus.Expired, assignment.Status);
        Assert.Equal(MissionStatus.SearchingProvider, mission.Status);
        Assert.Null(mission.ProviderId);
        Assert.Null(mission.ProviderAcceptedAt);
    }

    [Fact]
    public async Task ExpireDueAssignmentsAsync_WhenAssignmentIsStillOpen_DoesNothing()
    {
        await using var db = CreateDbContext();
        var assignment = new ProviderMissionAssignment(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(2));

        db.ProviderMissionAssignments.Add(assignment);
        await db.SaveChangesAsync();

        var result = await new ProviderAssignmentExpirationService(db).ExpireDueAssignmentsAsync(
            DateTimeOffset.UtcNow,
            batchSize: 10,
            CancellationToken.None);

        Assert.Equal(0, result.ExpiredAssignmentCount);
        Assert.Equal(ProviderMissionAssignmentStatus.Offered, assignment.Status);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }
}
