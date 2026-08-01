using HomeService.Application.Clients;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class ClientMissionListServiceTests
{
    [Fact]
    public async Task ListAsync_SemanticFiltersKeepActiveAndCancelledMissionsSeparate()
    {
        await using var db = CreateDbContext();
        var customer = new CustomerProfile("Aya", "Kone", "+2250700000000");
        var service = new Service("Barbier", "Soins homme", createdByCompanyId: null);
        var activeMission = CreateMission(customer.Id, service.Id);
        var cancelledMission = CreateMission(customer.Id, service.Id);
        cancelledMission.CancelByCustomer(0);
        db.Customers.Add(customer);
        db.Services.Add(service);
        db.Missions.AddRange(activeMission, cancelledMission);
        await db.SaveChangesAsync();
        var sut = new ClientMissionListService(db);

        var active = await sut.ListAsync(customer.Id, null, "Active", CancellationToken.None);
        var cancelled = await sut.ListAsync(customer.Id, null, "Cancelled", CancellationToken.None);

        Assert.Equal(activeMission.Id, Assert.Single(active.Missions).MissionId);
        Assert.Equal(cancelledMission.Id, Assert.Single(cancelled.Missions).MissionId);
    }

    private static Mission CreateMission(Guid customerId, Guid serviceId)
        => new(
            customerId,
            serviceId,
            MissionMode.Instant,
            PaymentMethod.MobileMoney,
            scheduledFor: null,
            estimatedDurationMinutes: 60,
            description: "Test",
            requiresCompanyQuote: true);

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new HomeServiceDbContext(options);
    }
}
