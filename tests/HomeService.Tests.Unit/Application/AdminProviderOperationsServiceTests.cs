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
                AuditActor.Admin(),
                new AuditRequestContext("127.0.0.1", "tests", "provider-approve"),
                CancellationToken.None);

            Assert.Equal(AdminProviderOperationStatus.Ok, result.Status);
            Assert.Equal(ProviderStatus.Approved, result.Provider!.Status);
            var log = Assert.Single(db.AuditLogEntries);
            Assert.Equal("AdminProviderApproved", log.Action);
            Assert.Equal(nameof(ProviderProfile), log.EntityType);
            Assert.Equal(providerId, log.EntityId);
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
