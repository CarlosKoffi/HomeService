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

    [Fact]
    public async Task ListAsync_CompletedMission_ExposesReorderContextForSameCompany()
    {
        await using var db = CreateDbContext();
        var customer = new CustomerProfile("Aya", "Kone", "+2250700000000");
        var service = new Service("Climatisation", null, createdByCompanyId: null);
        var company = new Company("Clim Pro", "+2250701111111", "ops@climpro.ci");
        company.Approve();
        var providerId = Guid.NewGuid();
        var mission = CreateMission(customer.Id, service.Id);
        mission.AssignWithCompanyQuote(providerId, company.Id, 25_000, 30_000, null);
        mission.MarkProviderAccepted(providerId, company.Id);
        mission.AcceptCompanyQuote();
        mission.ConfirmByCustomer(3_000, 0, 1200, customerServiceFeeAmount: 1_000,
            customerServiceFeeRateBasisPoints: 400, customerTotalAmount: 26_000,
            commissionableAmount: 25_000, isFirstCustomerCompanyOrder: true);
        mission.Start(providerId, company.Id);
        mission.Complete(60);
        db.AddRange(customer, service, company, mission);
        await db.SaveChangesAsync();

        var result = await new ClientMissionListService(db)
            .ListAsync(customer.Id, null, "Past", CancellationToken.None);

        var row = Assert.Single(result.Missions);
        Assert.True(row.CanReorder);
        Assert.Equal(company.Id, row.CompanyId);
        Assert.Equal(company.Name, row.CompanyName);
        Assert.Equal(service.Id, row.ServiceId);
        Assert.Equal(26_000, row.Amount);
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
