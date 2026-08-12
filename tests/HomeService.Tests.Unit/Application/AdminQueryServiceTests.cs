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
        Assert.Equal(4_500, dashboard.PlatformCommissionAmount);
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
        mission.MarkProviderAccepted(mission.ProviderId!.Value, company.Id);
        mission.ConfirmByCustomer(
            platformCommissionAmount: 750,
            transportFeeAmount: 0,
            platformCommissionRateBasisPoints: 1500);
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
            line.LineType == MissionFinancialLineType.PlatformCommission.ToString()
            && line.Label == "Commission w\u00E9l\u00E9");
        Assert.Contains(missionDetail.FinancialLines, line =>
            line.Label == "Avoir commercial"
            && line.Amount == -500);
    }

    [Fact]
    public async Task MissionQueries_WhenSearchingByMissionNumber_ReturnsSupportReadySummaryAndDetail()
    {
        await using var db = CreateDbContext();
        var company = new Company("CI Home Service", "0707000001", "contact@cihome.ci");
        company.Approve();
        var provider = new ProviderProfile(
            company.Id,
            "Awa",
            "Konate",
            "0707000002",
            "awa.konate@example.ci",
            new DateOnly(1992, 3, 14),
            "Cocody",
            ProviderGender.Female,
            ProviderEmploymentType.CompanyEmployee,
            yearsOfExperience: 5,
            missionLatitude: 5.360m,
            missionLongitude: -4.008m,
            missionRadiusKm: 8);
        provider.Approve();
        var service = new Service("Plomberie", "Depannage plomberie", createdByCompanyId: null);
        var customer = new CustomerProfile("Jean", "Kouassi", "0707000003");
        var mission = new Mission(
            customer.Id,
            service.Id,
            MissionMode.Scheduled,
            PaymentMethod.MobileMoney,
            DateTimeOffset.UtcNow.AddHours(4),
            estimatedDurationMinutes: 120,
            description: "Fuite sous evier avec piece a remplacer");
        mission.SetServiceLocation("Cocody Angre 7e tranche", 5.372m, -3.996m);
        mission.AssignWithCompanyQuote(
            provider.Id,
            company.Id,
            quotedAmount: 18_000,
            maxAllowedAmount: 20_000,
            overMaxJustification: null,
            partsEstimateAmount: 3_500,
            partsDescription: "Flexible et joint");
        mission.MarkProviderAccepted(provider.Id, company.Id);
        mission.AcceptCompanyQuote();
        mission.ConfirmByCustomer(
            platformCommissionAmount: 2_700,
            transportFeeAmount: 1_000,
            platformCommissionRateBasisPoints: 1500);

        db.Companies.Add(company);
        db.Providers.Add(provider);
        db.Services.Add(service);
        db.Customers.Add(customer);
        db.Missions.Add(mission);
        await db.SaveChangesAsync();

        var serviceUnderTest = new AdminQueryService(db);

        var missionList = await serviceUnderTest.ListMissionsAsync(
            status: MissionStatus.Accepted.ToString(),
            search: mission.MissionNumber,
            CancellationToken.None);
        var summary = Assert.Single(missionList.Items);
        var missionDetail = await serviceUnderTest.GetMissionAsync(mission.Id, CancellationToken.None);

        Assert.Equal(mission.Id, summary.Id);
        Assert.Equal(mission.MissionNumber, summary.MissionNumber);
        Assert.Equal("CI Home Service", summary.CompanyName);
        Assert.Equal("Jean Kouassi", summary.CustomerName);
        Assert.Equal("Awa Konate", summary.ProviderName);
        Assert.Equal(PaymentStatus.Authorized.ToString(), summary.PaymentStatus);
        Assert.Equal(PaymentMethod.MobileMoney.ToString(), summary.PaymentMethod);
        Assert.Equal(18_000, summary.Amount);
        Assert.Equal("Cocody Angre 7e tranche", summary.ServiceAddress);
        Assert.NotNull(missionDetail);
        Assert.Equal(mission.MissionNumber, missionDetail.MissionNumber);
        Assert.Equal(18_000, missionDetail.CompanyQuotedAmount);
        Assert.Equal(3_500, missionDetail.PartsEstimateAmount);
        Assert.Equal(2_700, missionDetail.PlatformCommissionAmount);
        Assert.Equal(15_300, missionDetail.CompanyPayoutAmount);
        Assert.Equal(1_000, missionDetail.TransportFeeAmount);
        Assert.True(missionDetail.CanRevealContactDetails);
        Assert.Contains(missionDetail.FinancialLines, line =>
            line.LineType == MissionFinancialLineType.PartsEstimate.ToString()
            && line.Amount == 3_500);
        Assert.Contains(missionDetail.FinancialLines, line =>
            line.LineType == MissionFinancialLineType.PlatformCommission.ToString()
            && line.Amount == 2_700);
        Assert.Contains(missionDetail.FinancialLines, line =>
            line.LineType == MissionFinancialLineType.CompanyPayout.ToString()
            && line.Amount == 15_300);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }
}
