using HomeService.Application.Admin;
using HomeService.Application.Auditing;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class AdminMissionOperationsServiceTests
{
    [Fact]
    public async Task CancelAsync_WhenNoteMissing_IsRejected()
    {
        await using var db = CreateDbContext();
        var mission = await SeedAcceptedMissionAsync(db);
        var sut = new AdminMissionOperationsService(db);

        var result = await sut.CancelAsync(
            mission.Id,
            "Other",
            " ",
            null,
            AuditActor.Admin(),
            null,
            CancellationToken.None);

        Assert.Equal(AdminMissionOperationStatus.ValidationFailed, result.Status);
        Assert.Equal(MissionStatus.Accepted, mission.Status);
        Assert.Empty(await db.AuditLogEntries.ToListAsync());
    }

    [Fact]
    public async Task CancelAsync_WhenAuthorizedMissionIsCancelled_RefundsAndAudits()
    {
        await using var db = CreateDbContext();
        var mission = await SeedAcceptedMissionAsync(db);
        mission.ConfirmByCustomer(
            platformCommissionAmount: 1_500,
            transportFeeAmount: 0,
            platformCommissionRateBasisPoints: 1_500);
        await db.SaveChangesAsync();
        var sut = new AdminMissionOperationsService(db);

        var result = await sut.CancelAsync(
            mission.Id,
            "CustomerAbsent",
            "Client injoignable apres plusieurs tentatives",
            null,
            AuditActor.Admin(),
            null,
            CancellationToken.None);

        Assert.Equal(AdminMissionOperationStatus.Ok, result.Status);
        Assert.Equal(MissionStatus.Cancelled, mission.Status);
        Assert.Equal(PaymentStatus.Refunded, mission.PaymentStatus);
        Assert.Equal(10_000, mission.RefundAmount);
        Assert.Equal(MissionCancellationActor.Admin, mission.CancelledBy);
        Assert.Equal("Client injoignable apres plusieurs tentatives", mission.CancellationComment);
        Assert.Single(await db.AuditLogEntries.ToListAsync());
    }

    [Fact]
    public async Task CancelAsync_WhenAdminProvidesCancellationFee_UsesItForRefund()
    {
        await using var db = CreateDbContext();
        var mission = await SeedAcceptedMissionAsync(db);
        mission.ConfirmByCustomer(
            platformCommissionAmount: 1_500,
            transportFeeAmount: 0,
            platformCommissionRateBasisPoints: 1_500);
        await db.SaveChangesAsync();
        var sut = new AdminMissionOperationsService(db);

        var result = await sut.CancelAsync(
            mission.Id,
            "Other",
            "Annulation avec frais partiels",
            2_500,
            AuditActor.Admin(),
            null,
            CancellationToken.None);

        Assert.Equal(AdminMissionOperationStatus.Ok, result.Status);
        Assert.Equal(MissionStatus.Cancelled, mission.Status);
        Assert.Equal(2_500, mission.CancellationFeeAmount);
        Assert.Equal(7_500, mission.RefundAmount);
    }

    [Fact]
    public async Task CancelAsync_WhenAdminFeeExceedsMissionAmount_IsRejected()
    {
        await using var db = CreateDbContext();
        var mission = await SeedAcceptedMissionAsync(db);
        mission.ConfirmByCustomer(
            platformCommissionAmount: 1_500,
            transportFeeAmount: 0,
            platformCommissionRateBasisPoints: 1_500);
        await db.SaveChangesAsync();
        var sut = new AdminMissionOperationsService(db);

        var result = await sut.CancelAsync(
            mission.Id,
            "Other",
            "Frais trop eleves",
            20_000,
            AuditActor.Admin(),
            null,
            CancellationToken.None);

        Assert.Equal(AdminMissionOperationStatus.ValidationFailed, result.Status);
        Assert.Equal(MissionStatus.Accepted, mission.Status);
        Assert.Equal(0, mission.CancellationFeeAmount);
        Assert.Equal(0, mission.RefundAmount);
    }

    private static async Task<Mission> SeedAcceptedMissionAsync(HomeServiceDbContext db)
    {
        var company = new Company("Entreprise Test", "+2250700000000", "contact@example.ci");
        company.Approve();
        var service = new Service("Menage a domicile", "Nettoyage residentiel", createdByCompanyId: null);
        var customer = new CustomerProfile("Awa", "Kone", "+2250700000001");
        var provider = new ProviderProfile(
            company.Id,
            "Mamadou",
            "Diallo",
            "+2250700000002",
            "mamadou@example.ci",
            new DateOnly(1995, 4, 12),
            "Cocody",
            ProviderGender.Male,
            ProviderEmploymentType.CompanyEmployee,
            yearsOfExperience: 4,
            missionLatitude: null,
            missionLongitude: null,
            missionRadiusKm: 5);
        provider.Approve();

        var mission = new Mission(
            customer.Id,
            service.Id,
            MissionMode.Instant,
            PaymentMethod.MobileMoney,
            scheduledFor: DateTimeOffset.UtcNow.AddHours(2),
            estimatedDurationMinutes: 120,
            description: "Grand nettoyage");
        mission.AssignWithCompanyQuote(
            provider.Id,
            company.Id,
            quotedAmount: 10_000,
            maxAllowedAmount: 15_000,
            overMaxJustification: null);
        mission.MarkProviderAccepted(provider.Id, company.Id);

        db.Companies.Add(company);
        db.Services.Add(service);
        db.Customers.Add(customer);
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
}
