using HomeService.Application.Admin;
using HomeService.Application.Auditing;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HomeService.Tests.Unit.Application;

public sealed class AdminProviderOperationsServiceTests
{
    [Fact]
    public async Task ApproveAsync_WhenProviderIsReady_ApprovesPersistsAndAudits()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var databaseRoot = new InMemoryDatabaseRoot();
        Guid providerId;

        await using (var db = CreateDbContext(databaseName, databaseRoot))
        {
            var scenario = CreateReadyProviderScenario();
            db.Companies.Add(scenario.Company);
            db.Services.Add(scenario.Service);
            db.Providers.Add(scenario.Provider);
            await db.SaveChangesAsync();
            providerId = scenario.Provider.Id;

            var result = await new AdminProviderOperationsService(db).ApproveAsync(
                providerId,
                "Identite et test metier valides",
                AuditActor.Admin(),
                new AuditRequestContext("127.0.0.1", "tests", "provider-approve"),
                CancellationToken.None);

            Assert.Equal(AdminProviderOperationStatus.Ok, result.Status);
            Assert.Equal(ProviderStatus.Approved, result.Provider!.Status);
            var log = Assert.Single(db.AuditLogEntries);
            Assert.Equal("AdminProviderApproved", log.Action);
            Assert.Equal(nameof(ProviderProfile), log.EntityType);
            Assert.Equal(providerId, log.EntityId);
            Assert.Equal("Identite et test metier valides", log.Summary);
            Assert.Equal("provider-approve", log.CorrelationId);
        }

        await using (var db = CreateDbContext(databaseName, databaseRoot))
        {
            var provider = await db.Providers.SingleAsync(item => item.Id == providerId);
            Assert.Equal(ProviderStatus.Approved, provider.Status);
        }
    }

    [Fact]
    public async Task SuspendAsync_WhenProviderIsApproved_SuspendsPersistsAndAuditsNote()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var databaseRoot = new InMemoryDatabaseRoot();
        Guid providerId;

        await using (var db = CreateDbContext(databaseName, databaseRoot))
        {
            var scenario = CreateReadyProviderScenario();
            scenario.Provider.Approve();
            db.Companies.Add(scenario.Company);
            db.Services.Add(scenario.Service);
            db.Providers.Add(scenario.Provider);
            await db.SaveChangesAsync();
            providerId = scenario.Provider.Id;

            var result = await new AdminProviderOperationsService(db).SuspendAsync(
                providerId,
                "Documents a reverifier",
                AuditActor.Admin(),
                new AuditRequestContext("127.0.0.1", "tests", "provider-suspend"),
                CancellationToken.None);

            Assert.Equal(AdminProviderOperationStatus.Ok, result.Status);
            Assert.Equal(ProviderStatus.SuspendedByPlatform, result.Provider!.Status);
            var log = Assert.Single(db.AuditLogEntries);
            Assert.Equal("AdminProviderSuspended", log.Action);
            Assert.Equal("Documents a reverifier", log.Summary);
            Assert.Equal("provider-suspend", log.CorrelationId);
        }

        await using (var db = CreateDbContext(databaseName, databaseRoot))
        {
            var provider = await db.Providers.SingleAsync(item => item.Id == providerId);
            Assert.Equal(ProviderStatus.SuspendedByPlatform, provider.Status);
        }
    }

    [Fact]
    public async Task ApproveAsync_WhenProviderHasNoCompany_ReturnsValidationFailedWithoutAudit()
    {
        await using var db = CreateDbContext(Guid.NewGuid().ToString("N"), new InMemoryDatabaseRoot());
        var provider = new ProviderProfile(
            "Malou",
            "Diallo",
            "+2250700000000",
            "malou@wele.ci",
            new DateOnly(1996, 8, 18),
            "Yopougon",
            ProviderGender.Male,
            4,
            null,
            null,
            5);
        db.Providers.Add(provider);
        await db.SaveChangesAsync();

        var result = await new AdminProviderOperationsService(db).ApproveAsync(
            provider.Id,
            "Validation admin",
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "tests", "provider-no-company"),
            CancellationToken.None);

        Assert.Equal(AdminProviderOperationStatus.ValidationFailed, result.Status);
        Assert.Equal("Le prestataire doit etre rattache a une entreprise avant validation.", result.Message);
        Assert.Empty(db.AuditLogEntries);
    }

    [Fact]
    public async Task ApproveAsync_WhenProviderHasNoActiveService_ReturnsValidationFailedWithoutAudit()
    {
        await using var db = CreateDbContext(Guid.NewGuid().ToString("N"), new InMemoryDatabaseRoot());
        var scenario = CreateReadyProviderScenario();
        db.Companies.Add(scenario.Company);
        db.Services.Add(scenario.Service);
        db.Providers.Add(scenario.Provider);
        await db.SaveChangesAsync();
        var service = scenario.Provider.Services.Single();
        service.Deactivate();
        await db.SaveChangesAsync();

        var result = await new AdminProviderOperationsService(db).ApproveAsync(
            scenario.Provider.Id,
            "Validation admin",
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "tests", "provider-no-service"),
            CancellationToken.None);

        Assert.Equal(AdminProviderOperationStatus.ValidationFailed, result.Status);
        Assert.Equal("Ajoutez au moins un service actif avant validation.", result.Message);
        Assert.Empty(db.AuditLogEntries);
    }

    [Fact]
    public async Task ApproveAsync_WhenProviderHasNoIdentityDocument_ReturnsValidationFailedWithoutAudit()
    {
        await using var db = CreateDbContext(Guid.NewGuid().ToString("N"), new InMemoryDatabaseRoot());
        var company = new Company("CI Home Service", "0700000000", "contact@ci.ci");
        company.Approve();
        var service = new Service("Menage a domicile", null, null);
        var provider = new ProviderProfile(
            company.Id,
            "Awa",
            "Konate",
            "+2250102030405",
            "awa@wele.ci",
            new DateOnly(1994, 4, 12),
            "Cocody",
            ProviderGender.Female,
            ProviderEmploymentType.CompanyEmployee,
            5,
            null,
            null,
            5);
        provider.AddService(service.Id, ExperienceLevel.Confirmed);
        db.Companies.Add(company);
        db.Services.Add(service);
        db.Providers.Add(provider);
        await db.SaveChangesAsync();

        var result = await new AdminProviderOperationsService(db).ApproveAsync(
            provider.Id,
            "Validation admin",
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "tests", "provider-no-id"),
            CancellationToken.None);

        Assert.Equal(AdminProviderOperationStatus.ValidationFailed, result.Status);
        Assert.Equal("Ajoutez une piece d'identite avant validation.", result.Message);
        Assert.Empty(db.AuditLogEntries);
    }

    [Fact]
    public async Task SuspendAsync_WhenProviderIsAlreadySuspended_ReturnsValidationFailedWithoutAudit()
    {
        await using var db = CreateDbContext(Guid.NewGuid().ToString("N"), new InMemoryDatabaseRoot());
        var scenario = CreateReadyProviderScenario();
        scenario.Provider.SuspendByPlatform();
        db.Companies.Add(scenario.Company);
        db.Services.Add(scenario.Service);
        db.Providers.Add(scenario.Provider);
        await db.SaveChangesAsync();

        var result = await new AdminProviderOperationsService(db).SuspendAsync(
            scenario.Provider.Id,
            "Nouvelle suspension",
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "tests", "provider-already-suspended"),
            CancellationToken.None);

        Assert.Equal(AdminProviderOperationStatus.ValidationFailed, result.Status);
        Assert.Equal("Ce prestataire est deja suspendu par la plateforme.", result.Message);
        Assert.Empty(db.AuditLogEntries);
    }

    [Fact]
    public async Task SetAvailabilityAsync_WhenProviderIsApproved_ForcesAvailabilityAndAudits()
    {
        await using var db = CreateDbContext(Guid.NewGuid().ToString("N"), new InMemoryDatabaseRoot());
        var scenario = CreateReadyProviderScenario();
        scenario.Provider.Approve();
        db.Companies.Add(scenario.Company);
        db.Services.Add(scenario.Service);
        db.Providers.Add(scenario.Provider);
        await db.SaveChangesAsync();

        var result = await new AdminProviderOperationsService(db).SetAvailabilityAsync(
            scenario.Provider.Id,
            true,
            "Disponible pour le test de mission",
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "tests", "provider-force-available"),
            CancellationToken.None);

        Assert.Equal(AdminProviderOperationStatus.Ok, result.Status);
        Assert.True(scenario.Provider.IsAvailable);
        Assert.Equal(5.3488m, scenario.Provider.CurrentLatitude);
        Assert.Equal(-4.0031m, scenario.Provider.CurrentLongitude);
        var log = Assert.Single(db.AuditLogEntries);
        Assert.Equal("AdminProviderAvailabilityForced", log.Action);
        Assert.Equal("Disponible pour le test de mission", log.Summary);
        Assert.Equal("provider-force-available", log.CorrelationId);
    }

    [Fact]
    public async Task SetAvailabilityAsync_WhenProviderIsNotApproved_ReturnsValidationFailed()
    {
        await using var db = CreateDbContext(Guid.NewGuid().ToString("N"), new InMemoryDatabaseRoot());
        var scenario = CreateReadyProviderScenario();
        db.Companies.Add(scenario.Company);
        db.Services.Add(scenario.Service);
        db.Providers.Add(scenario.Provider);
        await db.SaveChangesAsync();

        var result = await new AdminProviderOperationsService(db).SetAvailabilityAsync(
            scenario.Provider.Id,
            true,
            null,
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "tests", "provider-force-blocked"),
            CancellationToken.None);

        Assert.Equal(AdminProviderOperationStatus.ValidationFailed, result.Status);
        Assert.False(scenario.Provider.IsAvailable);
        Assert.Empty(db.AuditLogEntries);
    }

    [Fact]
    public async Task SetAvailabilityAsync_WhenProviderIsAvailable_CanForceUnavailable()
    {
        await using var db = CreateDbContext(Guid.NewGuid().ToString("N"), new InMemoryDatabaseRoot());
        var scenario = CreateReadyProviderScenario();
        scenario.Provider.Approve();
        scenario.Provider.SetAvailability(true, 5.35m, -4.01m);
        db.Companies.Add(scenario.Company);
        db.Services.Add(scenario.Service);
        db.Providers.Add(scenario.Provider);
        await db.SaveChangesAsync();

        var result = await new AdminProviderOperationsService(db).SetAvailabilityAsync(
            scenario.Provider.Id,
            false,
            null,
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "tests", "provider-force-unavailable"),
            CancellationToken.None);

        Assert.Equal(AdminProviderOperationStatus.Ok, result.Status);
        Assert.False(scenario.Provider.IsAvailable);
        Assert.Equal(5.35m, scenario.Provider.CurrentLatitude);
        Assert.Equal(-4.01m, scenario.Provider.CurrentLongitude);
    }

    private static ProviderScenario CreateReadyProviderScenario()
    {
        var company = new Company("CI Home Service", "0700000000", "contact@ci.ci");
        company.Approve();

        var service = new Service("Menage a domicile", null, null);
        var provider = new ProviderProfile(
            company.Id,
            "Awa",
            "Konate",
            "+2250102030405",
            "awa@wele.ci",
            new DateOnly(1994, 4, 12),
            "Cocody",
            ProviderGender.Female,
            ProviderEmploymentType.CompanyEmployee,
            5,
            null,
            null,
            5);
        provider.AddService(service.Id, ExperienceLevel.Confirmed);
        provider.AttachDocument(new ProviderDocument(
            provider.Id,
            ProviderDocumentType.IdentityDocument,
            "cni.png",
            "providers/awa/cni.png",
            "image/png"));

        return new ProviderScenario(company, service, provider);
    }

    private static HomeServiceDbContext CreateDbContext(string databaseName, InMemoryDatabaseRoot databaseRoot)
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(databaseName, databaseRoot)
            .Options;

        return new HomeServiceDbContext(options);
    }

    private sealed record ProviderScenario(Company Company, Service Service, ProviderProfile Provider);
}
