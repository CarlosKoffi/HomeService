using HomeService.Application.Clients;
using HomeService.Contracts.Clients;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class ClientMobileMoneyAccountServiceTests
{
    [Fact]
    public async Task AddMobileMoneyAccountAsync_WithSeveralNetworks_CreatesOneSelectableMethodPerNetwork()
    {
        await using var db = CreateDbContext();
        var customer = new CustomerProfile("Aya", "Kone", "+2250700000000");
        var orange = new PaymentProvider("orange-money", "Orange Money", PaymentMethod.MobileMoney, null, "/orange.png", 10);
        var mtn = new PaymentProvider("mtn-momo", "MTN MoMo", PaymentMethod.MobileMoney, null, "/mtn.png", 20);
        db.Customers.Add(customer);
        db.PaymentProviders.AddRange(orange, mtn);
        await db.SaveChangesAsync();

        var result = await new ClientProfileService(db).AddMobileMoneyAccountAsync(
            customer.Id,
            new CreateClientMobileMoneyAccountRequest(
                "+225 07 12 34 56 78",
                [orange.Id, mtn.Id],
                IsDefault: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("**** 5678", result.Response!.MaskedReference);
        Assert.Equal(2, result.Response.PaymentMethods.Count);
        Assert.Equal(new[] { "Orange Money", "MTN MoMo" }, result.Response.PaymentMethods.Select(method => method.PaymentProviderName));
        Assert.All(result.Response.PaymentMethods, method => Assert.Equal("**** 5678", method.MaskedReference));
        Assert.Single(result.Response.PaymentMethods, method => method.IsDefault);
        Assert.Equal(2, await db.CustomerPaymentMethods.CountAsync());
    }

    [Fact]
    public async Task AddMobileMoneyAccountAsync_WithCardProvider_IsRejectedWithoutCreatingAnything()
    {
        await using var db = CreateDbContext();
        var customer = new CustomerProfile("Aya", "Kone", "+2250700000000");
        var card = new PaymentProvider("bank-card", "Carte bancaire", PaymentMethod.Card, null, null, 50);
        db.Customers.Add(customer);
        db.PaymentProviders.Add(card);
        await db.SaveChangesAsync();

        var result = await new ClientProfileService(db).AddMobileMoneyAccountAsync(
            customer.Id,
            new CreateClientMobileMoneyAccountRequest("0700000000", [card.Id], IsDefault: true),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Empty(await db.CustomerPaymentMethods.ToListAsync());
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase($"client-mobile-money-{Guid.NewGuid():N}")
            .Options;
        return new HomeServiceDbContext(options);
    }
}
