using HomeService.Application.CompanyPortal;
using HomeService.Application.Companies;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class CompanyPortalDashboardServiceTests
{
    [Fact]
    public async Task GetAsync_ComputesProfileCompletionFromComplianceProvidersAndMissions()
    {
        await using var db = CreateDbContext();
        var company = CreateApprovedCompany();
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var service = new CompanyPortalDashboardService(db);
        var initial = await service.GetAsync(company.Id, userId: null, CancellationToken.None);

        Assert.True(initial.IsSuccess);
        Assert.Equal(25, initial.Response!.ProfileCompletionPercent);
        Assert.Equal("Documents de conformite", initial.Response.ProgressSteps[1].Label);
        Assert.False(initial.Response.ProgressSteps[1].IsDone);

        var application = CreateApplication(company);
        db.CompanyApplications.Add(application);
        foreach (var documentType in RequiredCompanyDocumentsPolicy.RequiredDocumentTypes)
        {
            var document = new CompanyApplicationDocument(
                application.Id,
                documentType,
                $"{documentType}.pdf",
                $"storage/{documentType}.pdf",
                "application/pdf");
            document.Approve();
            db.CompanyApplicationDocuments.Add(document);
        }

        var provider = CreateProvider(company.Id);
        db.Providers.Add(provider);
        await db.SaveChangesAsync();

        var withDocumentsAndProvider = await service.GetAsync(company.Id, userId: null, CancellationToken.None);

        Assert.True(withDocumentsAndProvider.IsSuccess);
        Assert.Equal(75, withDocumentsAndProvider.Response!.ProfileCompletionPercent);
        Assert.True(withDocumentsAndProvider.Response.ProgressSteps[1].IsDone);
        Assert.True(withDocumentsAndProvider.Response.ProgressSteps[2].IsDone);
        Assert.False(withDocumentsAndProvider.Response.ProgressSteps[3].IsDone);

        var customer = new CustomerProfile("Client", "Test", "+2250700000001");
        var catalogService = new Service("Menage a domicile", "Nettoyage residentiel", createdByCompanyId: null);
        db.Customers.Add(customer);
        db.Services.Add(catalogService);
        await db.SaveChangesAsync();
        var mission = new Mission(
            customer.Id,
            catalogService.Id,
            MissionMode.Scheduled,
            PaymentMethod.MobileMoney,
            DateTimeOffset.UtcNow.AddDays(1),
            estimatedDurationMinutes: 120,
            description: "Nettoyage complet");
        mission.AssignWithCompanyQuote(
            provider.Id,
            company.Id,
            quotedAmount: 15000,
            maxAllowedAmount: 20000,
            overMaxJustification: null);
        db.Missions.Add(mission);
        await db.SaveChangesAsync();

        var complete = await service.GetAsync(company.Id, userId: null, CancellationToken.None);

        Assert.True(complete.IsSuccess);
        Assert.Equal(100, complete.Response!.ProfileCompletionPercent);
        Assert.All(complete.Response.ProgressSteps, step => Assert.True(step.IsDone));
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }

    private static Company CreateApprovedCompany()
    {
        var company = new Company("Wélé Services", "+2250700000000", "contact@wele.ci");
        company.Approve();
        return company;
    }

    private static CompanyApplication CreateApplication(Company company)
    {
        var application = new CompanyApplication(
            company.Name,
            registrationNumber: null,
            "Abidjan",
            "Cocody",
            "Responsable",
            company.Email ?? "contact@wele.ci",
            company.PhoneNumber,
            "Menage",
            estimatedProviderCount: 1);
        application.LinkPendingCompany(company.Id);
        return application;
    }

    private static ProviderProfile CreateProvider(Guid companyId)
    {
        var provider = new ProviderProfile(
            companyId,
            "Awa",
            "Konate",
            "+2250700000002",
            "awa@wele.ci",
            new DateOnly(1995, 1, 1),
            "Cocody",
            ProviderGender.Female,
            ProviderEmploymentType.CompanyEmployee,
            yearsOfExperience: 4,
            missionLatitude: null,
            missionLongitude: null,
            missionRadiusKm: 5);
        provider.Approve();
        return provider;
    }
}
