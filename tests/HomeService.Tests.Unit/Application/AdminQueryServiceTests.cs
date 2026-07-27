using HomeService.Application.Admin;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class AdminQueryServiceTests
{
    [Fact]
    public async Task GetDashboardAsync_ShouldReturnOperationalSummary()
    {
        await using var db = CreateDbContext();
        var company = new Company("Ivoire Catering Group", "0707127068", "contact@ivoire.ci");
        company.Approve();
        var applicationToReview = new CompanyApplication(
            "Societe a verifier",
            registrationNumber: null,
            "Abidjan",
            "Cocody",
            "Awa Kone",
            "awa@example.ci",
            "0700000001",
            "Menage",
            estimatedProviderCount: 3);
        applicationToReview.MarkUnderReview();
        var applicationWaitingActivation = new CompanyApplication(
            "Societe approuvee",
            registrationNumber: null,
            "Abidjan",
            "Marcory",
            "Bakary Diallo",
            "bakary@example.ci",
            "0700000002",
            "Jardinage",
            estimatedProviderCount: 4);
        applicationWaitingActivation.Approve();
        var providerToReview = new ProviderProfile(
            company.Id,
            "Malou",
            "Diallo",
            "0700000003",
            "malou@example.ci",
            new DateOnly(1994, 4, 12),
            "Attecoube",
            ProviderGender.Male,
            ProviderEmploymentType.CompanyEmployee,
            yearsOfExperience: 4,
            missionLatitude: null,
            missionLongitude: null,
            missionRadiusKm: 5);
        providerToReview.SubmitForReview();
        var service = new Service("Plomberie", "Depannage plomberie", createdByCompanyId: null);
        var customer = new CustomerProfile("Client", "Test", "0700000004");
        var disputedMission = new Mission(
            customer.Id,
            service.Id,
            MissionMode.Scheduled,
            PaymentMethod.MobileMoney,
            DateTimeOffset.UtcNow.AddHours(3),
            estimatedDurationMinutes: 60,
            description: "Fuite evier");
        disputedMission.AssignWithCompanyQuote(
            providerToReview.Id,
            company.Id,
            quotedAmount: 10000,
            maxAllowedAmount: 12000,
            overMaxJustification: null);
        disputedMission.MarkProviderAccepted(providerToReview.Id, company.Id);
        disputedMission.ConfirmByCustomer(
            platformCommissionAmount: 1500,
            transportFeeAmount: 0,
            platformCommissionRateBasisPoints: 1500);
        disputedMission.MarkDisputed();
        var paidMission = new Mission(
            customer.Id,
            service.Id,
            MissionMode.Scheduled,
            PaymentMethod.Card,
            DateTimeOffset.UtcNow.AddDays(-1),
            estimatedDurationMinutes: 90,
            description: "Debouchage");
        paidMission.AssignWithCompanyQuote(
            providerToReview.Id,
            company.Id,
            quotedAmount: 20000,
            maxAllowedAmount: 25000,
            overMaxJustification: null);
        paidMission.MarkProviderAccepted(providerToReview.Id, company.Id);
        paidMission.ConfirmByCustomer(
            platformCommissionAmount: 3000,
            transportFeeAmount: 0,
            platformCommissionRateBasisPoints: 1500);
        paidMission.Start(providerToReview.Id, company.Id);
        paidMission.Complete(actualDurationMinutes: 90);
        paidMission.ValidateCompletionByCustomer();
        var portalNotification = new CompanyPortalNotification(
            company.Id,
            companyApplicationId: null,
            companyApplicationDocumentId: null,
            "document_review",
            "Document a corriger",
            "La piece d'identite doit etre remplacee.",
            "warning",
            "company-profile");
        var failedNotification = new NotificationOutboxMessage(
            NotificationChannel.Email,
            "contact@ivoire.ci",
            "Message test",
            "Contenu test",
            relatedEntityType: "Company",
            relatedEntityId: company.Id);
        failedNotification.MarkFailed("SMTP indisponible");

        db.Companies.Add(company);
        db.CompanyApplications.AddRange(applicationToReview, applicationWaitingActivation);
        db.Providers.Add(providerToReview);
        db.Services.Add(service);
        db.Customers.Add(customer);
        db.Missions.AddRange(disputedMission, paidMission);
        db.CompanyPortalNotifications.Add(portalNotification);
        db.NotificationOutboxMessages.Add(failedNotification);
        await db.SaveChangesAsync();

        var serviceUnderTest = new AdminQueryService(db);

        var dashboard = await serviceUnderTest.GetDashboardAsync(CancellationToken.None);

        Assert.Equal(1, dashboard.CompanyApplicationsToReview);
        Assert.Equal(1, dashboard.CompanyApplicationsWaitingActivation);
        Assert.Equal(1, dashboard.ActiveCompanies);
        Assert.Equal(1, dashboard.ProvidersToReview);
        Assert.Equal(1, dashboard.OpenMissions);
        Assert.Equal(1, dashboard.DisputedMissions);
        Assert.Equal(10_000, dashboard.PendingPaymentsAmount);
        Assert.Equal(3_000, dashboard.PlatformCommissionAmount);
        Assert.Equal(1, dashboard.UnreadCompanyPortalNotifications);
        Assert.Equal(1, dashboard.FailedExternalNotifications);
        Assert.Contains(dashboard.PriorityActions, action => action.Url == "missions?status=Disputed" && action.Count == 1);
    }

    [Fact]
    public async Task ListCompaniesAsync_ShouldFilterByDeclaredService()
    {
        await using var db = CreateDbContext();
        var laundryCompany = new Company("Pressing Abidjan", "0700000010", "pressing@example.ci");
        laundryCompany.UpdateOperations("Cocody", "Blanchisserie, Repassage");
        var gardeningCompany = new Company("Jardin Plus", "0700000011", "jardin@example.ci");
        gardeningCompany.UpdateOperations("Marcory", "Jardinage");

        db.Companies.AddRange(laundryCompany, gardeningCompany);
        await db.SaveChangesAsync();

        var serviceUnderTest = new AdminQueryService(db);

        var response = await serviceUnderTest.ListCompaniesAsync(
            status: null,
            search: null,
            service: "repassage",
            CancellationToken.None);

        var company = Assert.Single(response.Items);
        Assert.Equal(laundryCompany.Id, company.Id);
        Assert.Contains("Repassage", company.Services);
    }

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
