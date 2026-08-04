using HomeService.Application.Notifications;
using HomeService.Contracts.Notifications;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class MobileDeviceTokenServiceTests
{
    [Fact]
    public async Task RegisterAsync_WhenTokenIsNew_CreatesActiveToken()
    {
        await using var db = CreateDbContext();
        var sut = new MobileDeviceTokenService(db);
        var ownerId = Guid.NewGuid();

        var result = await sut.RegisterAsync(
            MobileDeviceOwnerType.Provider,
            ownerId,
            new RegisterMobileDeviceTokenRequest("fcm-token-1", "Android", "Samsung A15"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(db.MobileDeviceTokens);
        var token = await db.MobileDeviceTokens.SingleAsync();
        Assert.Equal(ownerId, token.OwnerId);
        Assert.Equal(MobileDevicePlatform.Android, token.Platform);
        Assert.True(token.IsActive);
    }

    [Fact]
    public async Task RegisterAsync_WhenSameOwnerRefreshesToken_DoesNotDuplicate()
    {
        await using var db = CreateDbContext();
        var sut = new MobileDeviceTokenService(db);
        var ownerId = Guid.NewGuid();

        await sut.RegisterAsync(
            MobileDeviceOwnerType.Provider,
            ownerId,
            new RegisterMobileDeviceTokenRequest("fcm-token-1", "Android", "Samsung A15"),
            CancellationToken.None);
        await sut.RegisterAsync(
            MobileDeviceOwnerType.Provider,
            ownerId,
            new RegisterMobileDeviceTokenRequest("fcm-token-1", "Ios", "iPhone"),
            CancellationToken.None);

        Assert.Single(db.MobileDeviceTokens);
        var token = await db.MobileDeviceTokens.SingleAsync();
        Assert.Equal(MobileDevicePlatform.Ios, token.Platform);
        Assert.Equal("iPhone", token.DeviceLabel);
    }

    [Fact]
    public async Task RegisterAsync_WhenSamePhoneChangesAccount_ReassignsTokenWithoutDuplicate()
    {
        await using var db = CreateDbContext();
        var sut = new MobileDeviceTokenService(db);
        var previousOwnerId = Guid.NewGuid();
        var currentOwnerId = Guid.NewGuid();

        await sut.RegisterAsync(
            MobileDeviceOwnerType.Customer,
            previousOwnerId,
            new RegisterMobileDeviceTokenRequest("shared-phone-token", "Android", "Compte precedent"),
            CancellationToken.None);
        var result = await sut.RegisterAsync(
            MobileDeviceOwnerType.Customer,
            currentOwnerId,
            new RegisterMobileDeviceTokenRequest("shared-phone-token", "Android", "Compte actuel"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var token = await db.MobileDeviceTokens.SingleAsync();
        Assert.Equal(currentOwnerId, token.OwnerId);
        Assert.Equal(MobileDeviceOwnerType.Customer, token.OwnerType);
        Assert.Equal("Compte actuel", token.DeviceLabel);
        Assert.True(token.IsActive);
        Assert.Null(token.DisabledAt);
        Assert.Null(token.FailureReason);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }
}
