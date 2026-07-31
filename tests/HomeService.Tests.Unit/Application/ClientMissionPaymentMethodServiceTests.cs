using HomeService.Application.Clients;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class ClientMissionPaymentMethodServiceTests
{
    [Fact]
    public async Task SelectAsync_WithOwnedActiveMethod_AttachesMethodToMission()
    {
        await using var db = CreateDbContext();
        var customer = new CustomerProfile("Awa", "Kone", "+2250700000001");
        var service = new Service("Menage", null, null);
        var method = new CustomerPaymentMethod(customer.Id, PaymentMethod.MobileMoney, "Orange Money", "•••• 0001", true);
        var mission = new Mission(customer.Id, service.Id, MissionMode.Instant, PaymentMethod.MobileMoney, null, 90);
        db.AddRange(customer, service, method, mission);
        await db.SaveChangesAsync();

        var result = await new ClientMissionPaymentMethodService(db)
            .SelectAsync(customer.Id, mission.Id, method.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(method.Id, mission.CustomerPaymentMethodId);
        Assert.Equal(PaymentMethod.MobileMoney, mission.PaymentMethod);
    }

    [Fact]
    public async Task SelectAsync_WithAnotherCustomersMethod_IsRejected()
    {
        await using var db = CreateDbContext();
        var customer = new CustomerProfile("Awa", "Kone", "+2250700000001");
        var other = new CustomerProfile("Jean", "Koffi", "+2250700000002");
        var service = new Service("Menage", null, null);
        var method = new CustomerPaymentMethod(other.Id, PaymentMethod.Card, "Carte", "•••• 4242", true);
        var mission = new Mission(customer.Id, service.Id, MissionMode.Instant, PaymentMethod.MobileMoney, null, 90);
        db.AddRange(customer, other, service, method, mission);
        await db.SaveChangesAsync();

        var result = await new ClientMissionPaymentMethodService(db)
            .SelectAsync(customer.Id, mission.Id, method.Id, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(mission.CustomerPaymentMethodId);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new HomeServiceDbContext(options);
    }
}
