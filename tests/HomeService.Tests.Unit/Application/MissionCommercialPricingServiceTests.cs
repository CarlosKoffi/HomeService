using HomeService.Application.Missions;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class MissionCommercialPricingServiceTests
{
    [Fact]
    public async Task CalculateAsync_FirstOrder_ExcludesPartsAndAppliesGlobalLaunchTier()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedScenarioAsync(db, includePreviousPaidOrder: false);

        var pricing = await new MissionCommercialPricingService(db)
            .CalculateAsync(scenario.Mission, 30_000, CancellationToken.None);

        Assert.True(pricing.IsFirstCustomerCompanyOrder);
        Assert.Equal(25_000, pricing.CommissionableAmount);
        Assert.Equal(1500, pricing.CompanyCommissionRateBasisPoints);
        Assert.Equal(3_750, pricing.CompanyCommissionAmount);
        Assert.Equal(1_000, pricing.CustomerServiceFeeAmount);
        Assert.Equal(31_000, pricing.CustomerTotalAmount);
        Assert.Equal(26_250, pricing.CompanyPayoutAmount);
    }

    [Fact]
    public async Task CalculateAsync_RepeatOrder_DoesNotReduceGlobalCompanyTier()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedScenarioAsync(db, includePreviousPaidOrder: true);

        var pricing = await new MissionCommercialPricingService(db)
            .CalculateAsync(scenario.Mission, 30_000, CancellationToken.None);

        Assert.False(pricing.IsFirstCustomerCompanyOrder);
        Assert.Equal(1500, pricing.CompanyCommissionRateBasisPoints);
        Assert.Equal(3_750, pricing.CompanyCommissionAmount);
        Assert.Equal(26_250, pricing.CompanyPayoutAmount);
        Assert.Equal(31_000, pricing.CustomerTotalAmount);
    }

    [Fact]
    public async Task CalculateAsync_FiftiethPaidMission_WithRequiredQuality_PromotesToFourteenPercent()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedScenarioAsync(db, includePreviousPaidOrder: false);
        db.CompanyCommissionTiers.AddRange(
            new CompanyCommissionTier("Lancement", 1, 1500, 10),
            new CompanyCommissionTier("Essor", 50, 1400, 20));

        var companyId = scenario.Mission.CompanyId!.Value;
        var customerId = scenario.Mission.CustomerId;
        var serviceId = scenario.Mission.ServiceId;
        var providerId = scenario.Mission.ProviderId!.Value;
        for (var index = 0; index < 49; index++)
        {
            var previous = CreateQuotedMission(customerId, serviceId, providerId, companyId);
            previous.MarkProviderAccepted(providerId, companyId);
            previous.AcceptCompanyQuote();
            previous.ConfirmByCustomer(3_750, 0, 1500, customerServiceFeeAmount: 1_000,
                customerServiceFeeRateBasisPoints: 400, customerTotalAmount: 31_000,
                commissionableAmount: 25_000, companyCommissionTierName: "Lancement",
                companyCommissionMissionSequence: index + 1);
            previous.Start(providerId, companyId);
            previous.Complete(60);
            previous.ValidateCompletionByCustomer();
            db.Missions.Add(previous);

            if (index < 10)
            {
                db.MissionReviews.Add(new MissionReview(
                    previous.Id, customerId, companyId, providerId, 5, 5, 5, 5, 5, "Excellent"));
            }
        }

        await db.SaveChangesAsync();
        var pricing = await new MissionCommercialPricingService(db)
            .CalculateAsync(scenario.Mission, 30_000, CancellationToken.None);

        Assert.Equal(1400, pricing.CompanyCommissionRateBasisPoints);
        Assert.Equal("Essor", pricing.CompanyCommissionTierName);
        Assert.Equal(50, pricing.CompanyCommissionMissionSequence);
        Assert.Equal(3_500, pricing.CompanyCommissionAmount);
    }

    private static async Task<PricingScenario> SeedScenarioAsync(
        HomeServiceDbContext db,
        bool includePreviousPaidOrder)
    {
        var service = new Service("Climatisation", null, null);
        var customer = new CustomerProfile("Aya", "Kone", "+2250700000000");
        var company = new Company("Entreprise test", "+2250701111111", "ops@test.ci");
        company.Approve();
        var providerId = Guid.NewGuid();

        db.Services.Add(service);
        db.Customers.Add(customer);
        db.Companies.Add(company);
        db.CommissionRules.AddRange(
            new CommissionRule("Premiere commande", CommissionRuleTarget.CompanyFirstCustomerOrder, 1200, 0, "XOF"),
            new CommissionRule("Commande recurrente", CommissionRuleTarget.CompanyRepeatCustomerOrder, 900, 0, "XOF"),
            new CommissionRule("Frais client", CommissionRuleTarget.CustomerServiceFee, 400, 0, "XOF"));

        if (includePreviousPaidOrder)
        {
            var previous = CreateQuotedMission(customer.Id, service.Id, providerId, company.Id);
            previous.MarkProviderAccepted(providerId, company.Id);
            previous.AcceptCompanyQuote();
            previous.ConfirmByCustomer(3_000, 0, 1200, customerServiceFeeAmount: 1_000,
                customerServiceFeeRateBasisPoints: 400, customerTotalAmount: 31_000,
                commissionableAmount: 25_000, isFirstCustomerCompanyOrder: true);
            db.Missions.Add(previous);
        }

        var mission = CreateQuotedMission(customer.Id, service.Id, providerId, company.Id);
        db.Missions.Add(mission);
        await db.SaveChangesAsync();
        return new PricingScenario(mission);
    }

    private static Mission CreateQuotedMission(
        Guid customerId,
        Guid serviceId,
        Guid providerId,
        Guid companyId)
    {
        var mission = new Mission(
            customerId,
            serviceId,
            MissionMode.Instant,
            PaymentMethod.MobileMoney,
            null,
            60,
            requiresCompanyQuote: true);
        mission.AssignWithCompanyQuote(
            providerId,
            companyId,
            quotedAmount: 30_000,
            maxAllowedAmount: 35_000,
            overMaxJustification: null,
            partsEstimateAmount: 5_000,
            partsDescription: "Piece de remplacement");
        return mission;
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new HomeServiceDbContext(options);
    }

    private sealed record PricingScenario(Mission Mission);
}
