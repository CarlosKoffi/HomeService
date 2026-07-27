using HomeService.Application.Admin;
using HomeService.Contracts.Admin;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class AdminMissionSettingsServiceTests
{
    [Fact]
    public async Task GetAsync_ReturnsCommissionRulesWithReadableRates()
    {
        await using var db = CreateDbContext();
        db.CommissionRules.Add(new CommissionRule("Commission wélé", CommissionRuleTarget.PlatformConnection, 1500, 0, "XOF"));
        await db.SaveChangesAsync();
        var sut = new AdminMissionSettingsService(db);

        var result = await sut.GetAsync(CancellationToken.None);

        var rule = Assert.Single(result.CommissionRules);
        Assert.Equal("Commission mise en relation", rule.TargetLabel);
        Assert.Equal(15m, rule.RatePercent);
        Assert.Equal("XOF", rule.Currency);
    }

    [Fact]
    public async Task UpdateCommissionRuleAsync_UpdatesExistingRule()
    {
        await using var db = CreateDbContext();
        var rule = new CommissionRule("Commission wélé", CommissionRuleTarget.PlatformConnection, 1500, 0, "XOF");
        db.CommissionRules.Add(rule);
        await db.SaveChangesAsync();
        var sut = new AdminMissionSettingsService(db);

        var result = await sut.UpdateCommissionRuleAsync(
            rule.Id,
            new UpdateAdminCommissionRuleRequest(1800, 500, "xof"),
            CancellationToken.None);

        Assert.Equal(AdminMissionSettingsOperationStatus.Ok, result.Status);
        var stored = await db.CommissionRules.SingleAsync();
        Assert.Equal(1800, stored.RateBasisPoints);
        Assert.Equal(500, stored.FixedAmount);
        Assert.Equal("XOF", stored.Currency);
    }

    [Fact]
    public async Task UpdateCommissionRuleAsync_WhenRateIsInvalid_ReturnsValidationFailed()
    {
        await using var db = CreateDbContext();
        var rule = new CommissionRule("Commission wélé", CommissionRuleTarget.PlatformConnection, 1500, 0, "XOF");
        db.CommissionRules.Add(rule);
        await db.SaveChangesAsync();
        var sut = new AdminMissionSettingsService(db);

        var result = await sut.UpdateCommissionRuleAsync(
            rule.Id,
            new UpdateAdminCommissionRuleRequest(12000, 0, "XOF"),
            CancellationToken.None);

        Assert.Equal(AdminMissionSettingsOperationStatus.ValidationFailed, result.Status);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }
}
