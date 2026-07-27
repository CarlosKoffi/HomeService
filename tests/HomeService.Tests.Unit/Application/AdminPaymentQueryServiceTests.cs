using HomeService.Application.Admin;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class AdminPaymentQueryServiceTests
{
    [Fact]
    public async Task ListPaymentsAsync_IncludesAuthorizedPlatformCommissionInAdminRevenue()
    {
        await using var db = CreateDbContext();
        var company = new Company("Entreprise Test", "+2250700000000", "contact@example.ci");
        var service = new Service("Menage a domicile", "Nettoyage residentiel", createdByCompanyId: null);
        var prestation = service.AddPrestation("Repassage", "Linge repasse", 1, 5_000, 8_000, "XOF");
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

        var mission = new Mission(
            customer.Id,
            service.Id,
            MissionMode.Instant,
            PaymentMethod.MobileMoney,
            scheduledFor: DateTimeOffset.UtcNow.AddHours(2),
            estimatedDurationMinutes: 120,
            servicePrestationId: prestation.Id,
            description: "Grand nettoyage");
        mission.AssignWithCompanyQuote(
            provider.Id,
            company.Id,
            quotedAmount: 10_000,
            maxAllowedAmount: 15_000,
            overMaxJustification: null);
        mission.MarkProviderAccepted(provider.Id, company.Id);
        mission.ConfirmByCustomer(
            platformCommissionAmount: 1_500,
            transportFeeAmount: 0,
            platformCommissionRateBasisPoints: 1_500);

        db.Companies.Add(company);
        db.Services.Add(service);
        db.Customers.Add(customer);
        db.Providers.Add(provider);
        db.Missions.Add(mission);
        await db.SaveChangesAsync();

        var result = await new AdminQueryService(db).ListPaymentsAsync(
            period: "month",
            paymentStatus: null,
            paymentMethod: null,
            search: null,
            CancellationToken.None);

        Assert.Equal(1_500, result.Stats.PlatformCommissionAmount);
        Assert.Equal(10_000, result.Stats.PaidAmount);
        Assert.Equal(0, result.Stats.PendingAmount);
        Assert.Equal(8_500, result.Stats.CompanyPayoutAmount);
        Assert.Equal(1, result.Stats.TransactionCount);
        Assert.Contains(result.Items, item =>
            item.MissionNumber == mission.MissionNumber
            && item.PlatformCommissionAmount == 1_500
            && item.PrestationName == "Repassage"
            && item.PaymentStatus == nameof(PaymentStatus.Authorized));

        var searchResult = await new AdminQueryService(db).ListPaymentsAsync(
            period: "month",
            paymentStatus: null,
            paymentMethod: null,
            search: "repassage",
            CancellationToken.None);

        Assert.Single(searchResult.Items);
        Assert.Equal("Repassage", searchResult.Items[0].PrestationName);

        var missionNumberResult = await new AdminQueryService(db).ListPaymentsAsync(
            period: "month",
            paymentStatus: null,
            paymentMethod: null,
            search: mission.MissionNumber,
            CancellationToken.None);

        var missionNumberItem = Assert.Single(missionNumberResult.Items);
        Assert.Equal(mission.MissionNumber, missionNumberItem.MissionNumber);

        var collectedResult = await new AdminQueryService(db).ListPaymentsAsync(
            period: "month",
            paymentStatus: "Collected",
            paymentMethod: null,
            search: null,
            CancellationToken.None);

        var collectedItem = Assert.Single(collectedResult.Items);
        Assert.Equal(mission.MissionNumber, collectedItem.MissionNumber);
        Assert.Equal(nameof(PaymentStatus.Authorized), collectedItem.PaymentStatus);
    }

    [Fact]
    public async Task ListPaymentsAsync_WhenMoreThanDisplayLimit_CalculatesStatsOnAllMatchingPayments()
    {
        await using var db = CreateDbContext();
        var company = new Company("Entreprise Test", "+2250700000000", "contact@example.ci");
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

        db.Companies.Add(company);
        db.Services.Add(service);
        db.Customers.Add(customer);
        db.Providers.Add(provider);

        for (var index = 0; index < 301; index++)
        {
            var mission = new Mission(
                customer.Id,
                service.Id,
                MissionMode.Instant,
                PaymentMethod.MobileMoney,
                scheduledFor: DateTimeOffset.UtcNow.AddMinutes(index),
                estimatedDurationMinutes: 60,
                description: $"Mission {index}");
            mission.AssignWithCompanyQuote(
                provider.Id,
                company.Id,
                quotedAmount: 1_000,
                maxAllowedAmount: 2_000,
                overMaxJustification: null);
            mission.MarkProviderAccepted(provider.Id, company.Id);
            mission.ConfirmByCustomer(
                platformCommissionAmount: 150,
                transportFeeAmount: 0,
                platformCommissionRateBasisPoints: 1_500);
            db.Missions.Add(mission);
        }

        await db.SaveChangesAsync();

        var result = await new AdminQueryService(db).ListPaymentsAsync(
            period: "month",
            paymentStatus: null,
            paymentMethod: null,
            search: null,
            CancellationToken.None);

        Assert.Equal(300, result.Items.Count);
        Assert.Equal(301, result.Stats.TransactionCount);
        Assert.Equal(301_000, result.Stats.PaidAmount);
        Assert.Equal(45_150, result.Stats.PlatformCommissionAmount);
        Assert.Equal(255_850, result.Stats.CompanyPayoutAmount);
    }

    [Fact]
    public async Task ListPaymentsAsync_WhenMissionIsRefunded_TracksRefundWithoutInflatingCollectedRevenue()
    {
        await using var db = CreateDbContext();
        var company = new Company("Entreprise Test", "+2250700000000", "contact@example.ci");
        var service = new Service("Depannage auto", "Assistance vehicule", createdByCompanyId: null);
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

        var mission = new Mission(
            customer.Id,
            service.Id,
            MissionMode.Instant,
            PaymentMethod.Card,
            scheduledFor: DateTimeOffset.UtcNow.AddHours(-2),
            estimatedDurationMinutes: 60,
            description: "Batterie a verifier");
        mission.AssignWithCompanyQuote(
            provider.Id,
            company.Id,
            quotedAmount: 12_000,
            maxAllowedAmount: 15_000,
            overMaxJustification: null);
        mission.MarkProviderAccepted(provider.Id, company.Id);
        mission.ConfirmByCustomer(
            platformCommissionAmount: 1_800,
            transportFeeAmount: 0,
            platformCommissionRateBasisPoints: 1_500);
        mission.MarkDisputed();
        mission.ApplyDisputeRefund(12_000);
        mission.ResolveDispute();

        db.Companies.Add(company);
        db.Services.Add(service);
        db.Customers.Add(customer);
        db.Providers.Add(provider);
        db.Missions.Add(mission);
        await db.SaveChangesAsync();

        var result = await new AdminQueryService(db).ListPaymentsAsync(
            period: "month",
            paymentStatus: null,
            paymentMethod: null,
            search: null,
            CancellationToken.None);

        Assert.Equal(12_000, result.Stats.TotalAmount);
        Assert.Equal(0, result.Stats.PaidAmount);
        Assert.Equal(0, result.Stats.PlatformCommissionAmount);
        Assert.Equal(0, result.Stats.CompanyPayoutAmount);
        Assert.Equal(12_000, result.Stats.RefundAmount);
        Assert.Equal(12_000, result.Stats.DisputedAmount);
        Assert.Equal(12_000, result.Items.Single().RefundAmount);
        Assert.Equal(nameof(PaymentStatus.Refunded), result.Items.Single().PaymentStatus);

        var riskResult = await new AdminQueryService(db).ListPaymentsAsync(
            period: "month",
            paymentStatus: "Risk",
            paymentMethod: null,
            search: null,
            CancellationToken.None);

        var riskItem = Assert.Single(riskResult.Items);
        Assert.Equal(mission.MissionNumber, riskItem.MissionNumber);
        Assert.Equal(nameof(PaymentStatus.Refunded), riskItem.PaymentStatus);
    }

    [Fact]
    public async Task ListPaymentsAsync_WhenMissionIsCancelledByAdmin_TracksRefundAndCancellationFee()
    {
        await using var db = CreateDbContext();
        var company = new Company("Entreprise Test", "+2250700000000", "contact@example.ci");
        var service = new Service("Electricite", "Depannage electrique", createdByCompanyId: null);
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

        var mission = new Mission(
            customer.Id,
            service.Id,
            MissionMode.Instant,
            PaymentMethod.MobileMoney,
            scheduledFor: DateTimeOffset.UtcNow.AddHours(-1),
            estimatedDurationMinutes: 60,
            description: "Prise a reparer");
        mission.AssignWithCompanyQuote(
            provider.Id,
            company.Id,
            quotedAmount: 10_000,
            maxAllowedAmount: 15_000,
            overMaxJustification: null);
        mission.MarkProviderAccepted(provider.Id, company.Id);
        mission.ConfirmByCustomer(
            platformCommissionAmount: 1_500,
            transportFeeAmount: 0,
            platformCommissionRateBasisPoints: 1_500);
        mission.Cancel(
            MissionCancellationActor.Admin,
            MissionCancellationReason.Other,
            "Annulation admin avec frais",
            cancellationFeeAmount: 2_000,
            refundAmount: 8_000);

        db.Companies.Add(company);
        db.Services.Add(service);
        db.Customers.Add(customer);
        db.Providers.Add(provider);
        db.Missions.Add(mission);
        await db.SaveChangesAsync();

        var result = await new AdminQueryService(db).ListPaymentsAsync(
            period: "month",
            paymentStatus: null,
            paymentMethod: null,
            search: null,
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(10_000, result.Stats.TotalAmount);
        Assert.Equal(0, result.Stats.PaidAmount);
        Assert.Equal(0, result.Stats.PlatformCommissionAmount);
        Assert.Equal(0, result.Stats.CompanyPayoutAmount);
        Assert.Equal(8_000, result.Stats.RefundAmount);
        Assert.Equal(10_000, result.Stats.DisputedAmount);
        Assert.Equal(8_000, item.RefundAmount);
        Assert.Equal(2_000, item.CancellationFeeAmount);
        Assert.Equal(nameof(PaymentStatus.Refunded), item.PaymentStatus);
        Assert.Equal(nameof(MissionStatus.Cancelled), item.MissionStatus);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }
}
