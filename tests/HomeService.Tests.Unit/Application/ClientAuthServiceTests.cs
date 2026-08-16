using HomeService.Application.Clients;
using HomeService.Contracts.Clients;
using HomeService.Infrastructure.Data;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class ClientAuthServiceTests
{
    [Fact]
    public async Task Register_returns_explicit_name_errors_when_last_name_is_missing()
    {
        await using var db = CreateDbContext();
        var sut = new ClientAuthService(db);

        var result = await sut.RegisterAsync(new RegisterClientRequest(
            "Ajavon",
            string.Empty,
            "08544500564",
            "bruce.carl@gmail.com",
            "Carlos_198499"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Nom obligatoire.", result.Errors);
    }

    [Fact]
    public async Task RegisterAsync_CreatesCustomerAndSession()
    {
        await using var db = CreateDbContext();
        var sut = new ClientAuthService(db);

        var result = await sut.RegisterAsync(new RegisterClientRequest(
            "Aya",
            "Kone",
            "+225 07 00 00 00 00",
            "aya@wele.ci",
            "Testeur123",
            true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.False(string.IsNullOrWhiteSpace(result.Response.Token));
        Assert.Equal("+2250700000000", result.Response.PhoneNumber);
        Assert.Single(db.Customers);
        Assert.Single(db.CustomerSessions);
    }

    [Fact]
    public async Task LoginAsync_WithValidPassword_ReturnsSession()
    {
        await using var db = CreateDbContext();
        var sut = new ClientAuthService(db);
        await sut.RegisterAsync(new RegisterClientRequest(
            "Aya",
            "Kone",
            "+2250700000000",
            null,
            "Testeur123",
            true), CancellationToken.None);

        var result = await sut.LoginAsync(new LoginClientRequest("+2250700000000", "Testeur123", false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.Equal("+2250700000000", result.Response.PhoneNumber);
        Assert.Equal(2, await db.CustomerSessions.CountAsync());
    }

    [Fact]
    public async Task LoginAsync_WithInvalidPassword_ReturnsError()
    {
        await using var db = CreateDbContext();
        var sut = new ClientAuthService(db);
        await sut.RegisterAsync(new RegisterClientRequest(
            "Aya",
            "Kone",
            "+2250700000000",
            null,
            "Testeur123",
            true), CancellationToken.None);

        var result = await sut.LoginAsync(new LoginClientRequest("+2250700000000", "bad-password", false), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task Same_phone_can_register_one_personal_and_one_business_account()
    {
        await using var db = CreateDbContext();
        var sut = new ClientAuthService(db);
        var request = new RegisterClientRequest(
            "Aya",
            "Kone",
            "+2250700000000",
            "aya@wele.ci",
            "Testeur123",
            true);

        var personal = await sut.RegisterAsync(request, CancellationToken.None);
        var business = await sut.RegisterAsync(request, CancellationToken.None, CustomerAccountType.Business);
        var duplicateBusiness = await sut.RegisterAsync(request, CancellationToken.None, CustomerAccountType.Business);

        Assert.True(personal.IsSuccess);
        Assert.True(business.IsSuccess);
        Assert.False(duplicateBusiness.IsSuccess);
        Assert.Equal(2, await db.Customers.CountAsync());
        Assert.Contains(await db.Customers.ToListAsync(), item => item.AccountType == CustomerAccountType.Personal);
        Assert.Contains(await db.Customers.ToListAsync(), item => item.AccountType == CustomerAccountType.Business);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase($"client-auth-{Guid.NewGuid():N}")
            .Options;
        return new HomeServiceDbContext(options);
    }
}
