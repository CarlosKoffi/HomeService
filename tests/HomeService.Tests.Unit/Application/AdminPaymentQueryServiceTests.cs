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
        Assert.Equal(10_000, result.Stats.PendingAmount);
        Assert.Equal(1, result.Stats.TransactionCount);
        Assert.Contains(result.Items, item =>
            item.MissionNumber == mission.MissionNumber
            && item.PlatformCommissionAmount == 1_500
            && item.PaymentStatus == nameof(PaymentStatus.Authorized));
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }
}
