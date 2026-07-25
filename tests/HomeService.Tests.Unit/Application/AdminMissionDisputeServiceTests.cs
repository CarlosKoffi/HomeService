using HomeService.Application.Admin;
using HomeService.Application.Auditing;
using HomeService.Application.CompanyPortal;
using HomeService.Contracts.Missions;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class AdminMissionDisputeServiceTests
{
    [Fact]
    public async Task OpenAsync_WhenMissionIsOpen_CreatesStructuredDispute()
    {
        await using var db = CreateDbContext();
        var mission = await SeedMissionAsync(db);
        var sut = CreateService(db);

        var result = await sut.OpenAsync(
            mission.Id,
            "CustomerAbsent",
            "Client absent au rendez-vous",
            AuditActor.Admin(),
            null,
            CancellationToken.None);

        Assert.Equal(AdminMissionOperationStatus.Ok, result.Status);
        Assert.Equal(MissionStatus.Disputed, mission.Status);
        Assert.Equal(1, await db.MissionDisputes.CountAsync());
        Assert.Equal(1, await db.CompanyPortalNotifications.CountAsync());
        Assert.Equal(1, await db.AuditLogEntries.CountAsync());
    }

    [Fact]
    public async Task ResolveAsync_WhenDisputeOpen_ResolvesMissionAndDispute()
    {
        await using var db = CreateDbContext();
        var mission = await SeedMissionAsync(db);
        var sut = CreateService(db);
        await sut.OpenAsync(mission.Id, "Other", "Verification necessaire", AuditActor.Admin(), null, CancellationToken.None);

        var result = await sut.ResolveAsync(
            mission.Id,
            "PartialRefund",
            "Remboursement partiel valide",
            40,
            null,
            AuditActor.Admin(),
            null,
            CancellationToken.None);

        var dispute = await db.MissionDisputes.SingleAsync();
        Assert.Equal(AdminMissionOperationStatus.Ok, result.Status);
        Assert.Equal(MissionStatus.Resolved, mission.Status);
        Assert.Equal(MissionDisputeStatus.Resolved, dispute.Status);
        Assert.Equal(MissionDisputeResolution.PartialRefund, dispute.Resolution);
        Assert.Equal(4000, dispute.RefundPercentBasisPoints);
        Assert.Equal(800, dispute.RefundAmount);
        Assert.Equal(1, await db.MissionFinancialBreakdowns.CountAsync());
        Assert.Equal(2, await db.CompanyPortalNotifications.CountAsync());
        Assert.Equal(2, await db.AuditLogEntries.CountAsync());
    }

    [Fact]
    public async Task ResolveAsync_WhenFullRefundWithoutPercent_DefaultsToHundredPercent()
    {
        await using var db = CreateDbContext();
        var mission = await SeedMissionAsync(db);
        var sut = CreateService(db);
        await sut.OpenAsync(mission.Id, "Other", "Service conteste", AuditActor.Admin(), null, CancellationToken.None);

        var result = await sut.ResolveAsync(
            mission.Id,
            "RefundCustomer",
            "Remboursement complet accepte",
            null,
            null,
            AuditActor.Admin(),
            null,
            CancellationToken.None);

        var dispute = await db.MissionDisputes.SingleAsync();
        Assert.Equal(AdminMissionOperationStatus.Ok, result.Status);
        Assert.Equal(10000, dispute.RefundPercentBasisPoints);
        Assert.Equal(2000, dispute.RefundAmount);
    }

    [Fact]
    public async Task ResolveAsync_WhenNoOpenDispute_IsRejected()
    {
        await using var db = CreateDbContext();
        var mission = await SeedMissionAsync(db);
        var sut = CreateService(db);

        var result = await sut.ResolveAsync(
            mission.Id,
            "NoAction",
            "Rien a faire",
            null,
            null,
            AuditActor.Admin(),
            null,
            CancellationToken.None);

        Assert.Equal(AdminMissionOperationStatus.ValidationFailed, result.Status);
    }

    private static async Task<Mission> SeedMissionAsync(HomeServiceDbContext db)
    {
        var service = new Service("Menage", null, createdByCompanyId: null);
        var customer = new CustomerProfile("Aya", "Kone", "+2250700000000");
        var company = new Company("wele Services", "+2250701111111", "ops@wele.ci");
        company.Approve();
        var provider = new ProviderProfile(
            company.Id,
            "Awa",
            "Konate",
            "+2250702222222",
            "awa@wele.ci",
            new DateOnly(1994, 2, 3),
            "Cocody",
            ProviderGender.Female,
            ProviderEmploymentType.CompanyEmployee,
            5,
            5.348850m,
            -4.003150m,
            5);
        provider.Approve();
        var mission = new Mission(customer.Id, service.Id, MissionMode.Instant, PaymentMethod.MobileMoney, null, 60);
        mission.Assign(provider.Id, company.Id, 2000);

        db.Services.Add(service);
        db.Customers.Add(customer);
        db.Companies.Add(company);
        db.Providers.Add(provider);
        db.Missions.Add(mission);
        await db.SaveChangesAsync();

        return mission;
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }

    private static AdminMissionDisputeService CreateService(HomeServiceDbContext db)
        => new(db, new CompanyPortalNotificationWriter(db));
}
