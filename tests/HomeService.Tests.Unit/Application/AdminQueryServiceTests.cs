using HomeService.Application.Admin;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class AdminQueryServiceTests
{
    [Fact]
    public async Task MissionQueries_ShouldExposeServicePrestationNames()
    {
        await using var db = CreateDbContext();
        var company = new Company("Ivoire Catering Group", "0707127068", "contact@ivoire.ci");
        company.Approve();
        var service = new Service("Blanchisserie", "Entretien du linge", createdByCompanyId: null);
        var prestation = service.AddPrestation(
            "Repassage",
            "Repassage a domicile",
            sortOrder: 1,
            normalPriceAmount: 2500,
            premiumPriceAmount: 4500,
            currency: "XOF");
        var customer = new CustomerProfile("Awa", "Kone", "0700000001");
        var mission = new Mission(
            customer.Id,
            service.Id,
            MissionMode.Scheduled,
            PaymentMethod.MobileMoney,
            DateTimeOffset.UtcNow.AddDays(1),
            estimatedDurationMinutes: 90,
            servicePrestationId: prestation.Id,
            description: "Repasser chemises et draps");
        mission.AssignWithCompanyQuote(
            providerId: Guid.NewGuid(),
            companyId: company.Id,
            quotedAmount: 5000,
            maxAllowedAmount: 8000,
            overMaxJustification: null);
        var refundLine = new MissionFinancialBreakdown(
            mission.Id,
            MissionFinancialLineType.Refund,
            "Avoir commercial",
            -500,
            "XOF",
            80);

        db.Companies.Add(company);
        db.Services.Add(service);
        db.Customers.Add(customer);
        db.Missions.Add(mission);
        db.MissionFinancialBreakdowns.Add(refundLine);
        await db.SaveChangesAsync();

        var serviceUnderTest = new AdminQueryService(db);

        var companyDetail = await serviceUnderTest.GetCompanyAsync(company.Id, CancellationToken.None);
        var missionList = await serviceUnderTest.ListMissionsAsync(null, "repassage", CancellationToken.None);
        var missionDetail = await serviceUnderTest.GetMissionAsync(mission.Id, CancellationToken.None);

        Assert.NotNull(companyDetail);
        Assert.Contains(companyDetail.Missions, item => item.Id == mission.Id && item.PrestationName == "Repassage");
        Assert.Contains(missionList.Items, item => item.Id == mission.Id && item.PrestationName == "Repassage");
        Assert.NotNull(missionDetail);
        Assert.Equal("Repassage", missionDetail.PrestationName);
        Assert.Contains(missionDetail.FinancialLines, line =>
            line.LineType == MissionFinancialLineType.ServicePrice.ToString()
            && line.Amount == 5000);
        Assert.Contains(missionDetail.FinancialLines, line =>
            line.Label == "Avoir commercial"
            && line.Amount == -500);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }
}
