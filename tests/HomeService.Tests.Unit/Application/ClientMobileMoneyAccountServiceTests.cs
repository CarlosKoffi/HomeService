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

    [Fact]
    public async Task UpdateMobileMoneyAccountAsync_AddsAndRemovesNetworksWithoutChangingTheNumber()
    {
        await using var db = CreateDbContext();
        var customer = new CustomerProfile("Aya", "Kone", "+2250700000000");
        var orange = new PaymentProvider("orange-money", "Orange Money", PaymentMethod.MobileMoney, null, "/orange.png", 10);
        var mtn = new PaymentProvider("mtn-momo", "MTN MoMo", PaymentMethod.MobileMoney, null, "/mtn.png", 20);
        var moov = new PaymentProvider("moov-money", "Moov Money", PaymentMethod.MobileMoney, null, "/moov.png", 30);
        var orangeMethod = new CustomerPaymentMethod(
            customer.Id,
            orange.Id,
            PaymentMethod.MobileMoney,
            orange.Name,
            "**** 5678",
            isDefault: true);
        var mtnMethod = new CustomerPaymentMethod(
            customer.Id,
            mtn.Id,
            PaymentMethod.MobileMoney,
            mtn.Name,
            "**** 5678",
            isDefault: false);
        db.Customers.Add(customer);
        db.PaymentProviders.AddRange(orange, mtn, moov);
        db.CustomerPaymentMethods.AddRange(orangeMethod, mtnMethod);
        await db.SaveChangesAsync();

        var result = await new ClientProfileService(db).UpdateMobileMoneyAccountAsync(
            customer.Id,
            orangeMethod.Id,
            new UpdateClientMobileMoneyAccountRequest([mtn.Id, moov.Id]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("**** 5678", result.Response!.MaskedReference);
        Assert.Equal(new[] { "MTN MoMo", "Moov Money" }, result.Response.PaymentMethods.Select(method => method.PaymentProviderName));
        Assert.DoesNotContain(result.Response.PaymentMethods, method => method.PaymentProviderId == orange.Id);
        Assert.Single(result.Response.PaymentMethods, method => method.IsDefault);

        var stored = await db.CustomerPaymentMethods.OrderBy(method => method.Label).ToListAsync();
        Assert.False(stored.Single(method => method.PaymentProviderId == orange.Id).IsActive);
        Assert.True(stored.Single(method => method.PaymentProviderId == mtn.Id).IsActive);
        Assert.True(stored.Single(method => method.PaymentProviderId == moov.Id).IsActive);
        Assert.All(stored, method => Assert.Equal("**** 5678", method.MaskedReference));
    }

    [Fact]
    public async Task UpdateMobileMoneyAccountAsync_WithoutAnyNetwork_IsRejected()
    {
        await using var db = CreateDbContext();
        var customer = new CustomerProfile("Aya", "Kone", "+2250700000000");
        var orange = new PaymentProvider("orange-money", "Orange Money", PaymentMethod.MobileMoney, null, null, 10);
        var method = new CustomerPaymentMethod(
            customer.Id,
            orange.Id,
            PaymentMethod.MobileMoney,
            orange.Name,
            "**** 5678",
            isDefault: true);
        db.Customers.Add(customer);
        db.PaymentProviders.Add(orange);
        db.CustomerPaymentMethods.Add(method);
        await db.SaveChangesAsync();

        var result = await new ClientProfileService(db).UpdateMobileMoneyAccountAsync(
            customer.Id,
            method.Id,
            new UpdateClientMobileMoneyAccountRequest([]),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.True(method.IsActive);
        Assert.True(method.IsDefault);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase($"client-mobile-money-{Guid.NewGuid():N}")
            .Options;
        return new HomeServiceDbContext(options);
    }
}
