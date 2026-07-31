using HomeService.Application.Admin;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class AdminClientQueryServiceTests
{
    [Fact]
    public async Task List_and_detail_return_client_operational_data_without_sensitive_payment_values()
    {
        await using var db = CreateDbContext();
        var customer = new CustomerProfile("Aya", "Kone", "+2250700000001");
        customer.UpdateProfile("Aya", "Kone", "aya@example.ci");
        var service = new Service("Plomberie", "Depannage", null);
        var address = new CustomerAddress(customer.Id, "Maison", "Cocody Angre", 5.36m, -3.98m, true);
        var payment = new CustomerPaymentMethod(customer.Id, PaymentMethod.MobileMoney, "Orange Money", "**** 0001", true);
        var mission = new Mission(customer.Id, service.Id, MissionMode.Scheduled, PaymentMethod.MobileMoney, DateTimeOffset.UtcNow, 60);

        db.AddRange(customer, service, address, payment, mission);
        await db.SaveChangesAsync();

        var sut = new AdminClientQueryService(db);
        var list = await sut.ListAsync("aya", CancellationToken.None);
        var detail = await sut.GetAsync(customer.Id, CancellationToken.None);

        Assert.Single(list.Items);
        Assert.NotNull(detail);
        Assert.Equal("Aya Kone", detail.FullName);
        Assert.Equal("Cocody Angre", Assert.Single(detail.Addresses).AddressLine);
        Assert.Equal("**** 0001", Assert.Single(detail.PaymentMethods).MaskedReference);
        Assert.Single(detail.Missions);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase($"admin-client-{Guid.NewGuid():N}")
            .Options;
        return new HomeServiceDbContext(options);
    }
}
