using HomeService.Application.Admin;
using HomeService.Application.Auditing;
using HomeService.Contracts.Admin;
using HomeService.Contracts.Branding;
using HomeService.Contracts.Companies;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class AdminCompanyOperationsServiceTests
{
    [Fact]
    public async Task SuspendAsync_WhenCompanyIsApproved_PersistsStatusAndAudit()
    {
        await using var db = CreateDbContext();
        var company = new Company("Wélé Services", "+2250700000000", "contact@wele.ci");
        company.Approve();
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        var sut = new AdminCompanyOperationsService(db);

        var result = await sut.SuspendAsync(
            company.Id,
            "Controle qualite",
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "unit-tests", "company-suspend"),
            CancellationToken.None);

        Assert.Equal(AdminCompanyOperationStatus.Ok, result.Status);
        Assert.Equal(CompanyStatus.Suspended, (await db.Companies.SingleAsync(item => item.Id == company.Id)).Status);
        var audit = await db.AuditLogEntries.SingleAsync();
        Assert.Equal("AdminCompanySuspended", audit.Action);
        Assert.Equal(company.Id, audit.EntityId);
        Assert.Equal("company-suspend", audit.CorrelationId);
        Assert.Contains("Controle qualite", audit.Summary);
    }

    [Fact]
    public async Task ReactivateAsync_WhenCompanyIsSuspended_PersistsStatusAndAudit()
    {
        await using var db = CreateDbContext();
        var company = new Company("Wélé Services", "+2250700000000", "contact@wele.ci");
        company.Suspend();
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        var sut = new AdminCompanyOperationsService(db);

        var result = await sut.ReactivateAsync(
            company.Id,
            "Pieces regularisees",
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "unit-tests", "company-reactivate"),
            CancellationToken.None);

        Assert.Equal(AdminCompanyOperationStatus.Ok, result.Status);
        Assert.Equal(CompanyStatus.Approved, (await db.Companies.SingleAsync(item => item.Id == company.Id)).Status);
        var audit = await db.AuditLogEntries.SingleAsync();
        Assert.Equal("AdminCompanyReactivated", audit.Action);
        Assert.Equal(company.Id, audit.EntityId);
        Assert.Equal("company-reactivate", audit.CorrelationId);
    }

    [Fact]
    public async Task UpdateCompanyAssignmentModeAsync_WhenValid_PersistsModeAndAudit()
    {
        await using var db = CreateDbContext();
        var company = new Company("Wélé Services", "+2250700000000", "contact@wele.ci");
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        var sut = new AdminConfigurationService(db);

        var result = await sut.UpdateCompanyAssignmentModeAsync(
            company.Id,
            new UpdateCompanyAssignmentModeRequest(nameof(CompanyAssignmentMode.PlatformManaged)),
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "unit-tests", "assignment-mode"),
            CancellationToken.None);

        Assert.Equal(AdminConfigurationUpdateStatus.Ok, result.Status);
        Assert.Equal(CompanyAssignmentMode.PlatformManaged, (await db.Companies.SingleAsync(item => item.Id == company.Id)).AssignmentMode);
        var audit = await db.AuditLogEntries.SingleAsync();
        Assert.Equal("AdminCompanyAssignmentModeUpdated", audit.Action);
        Assert.Equal(company.Id, audit.EntityId);
        Assert.Equal("assignment-mode", audit.CorrelationId);
    }

    [Fact]
    public async Task UpdateCompanyDispatchSettingsAsync_WhenValid_PersistsSettingsAndAudit()
    {
        await using var db = CreateDbContext();
        var company = new Company("Wélé Services", "+2250700000000", "contact@wele.ci");
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        var sut = new AdminConfigurationService(db);

        var result = await sut.UpdateCompanyDispatchSettingsAsync(
            company.Id,
            new UpdateAdminCompanyDispatchSettingsRequest(25, true),
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "unit-tests", "dispatch-settings"),
            CancellationToken.None);

        Assert.Equal(AdminConfigurationUpdateStatus.Ok, result.Status);
        var savedCompany = await db.Companies.SingleAsync(item => item.Id == company.Id);
        Assert.Equal(25, savedCompany.MissionDispatchPriority);
        Assert.True(savedCompany.AcceptsUrgentMissions);
        var audit = await db.AuditLogEntries.SingleAsync();
        Assert.Equal("AdminCompanyDispatchSettingsUpdated", audit.Action);
        Assert.Equal(company.Id, audit.EntityId);
        Assert.Equal("dispatch-settings", audit.CorrelationId);
    }

    [Fact]
    public async Task UpdateCountryBrandingAsync_WhenBrandingIsNew_PersistsBrandingAndAudit()
    {
        await using var db = CreateDbContext();
        var country = new Country("CI", "Cote d'Ivoire", "XOF", true);
        db.Countries.Add(country);
        await db.SaveChangesAsync();
        var sut = new AdminConfigurationService(db);

        var result = await sut.UpdateCountryBrandingAsync(
            "ci",
            ValidBrandingRequest("wele"),
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "unit-tests", "branding-create"),
            CancellationToken.None);

        Assert.Equal(AdminConfigurationUpdateStatus.Ok, result.Status);
        var branding = await db.CountryBrandings.SingleAsync();
        Assert.Equal(country.Id, branding.CountryId);
        Assert.Equal("wele", branding.BrandName);
        var audit = await db.AuditLogEntries.SingleAsync();
        Assert.Equal("AdminCountryBrandingUpdated", audit.Action);
        Assert.Equal(branding.Id, audit.EntityId);
        Assert.Equal("branding-create", audit.CorrelationId);
    }

    [Fact]
    public async Task UpdateCountryBrandingAsync_WhenBrandingExists_UpdatesBrandingAndAudit()
    {
        await using var db = CreateDbContext();
        var country = new Country("CI", "Cote d'Ivoire", "XOF", true);
        var branding = new CountryBranding(
            country.Id,
            "Kaza",
            "#0a66c2",
            "#ffffff",
            "#111111",
            "Ancien titre",
            "Ancien sous titre",
            null,
            "apple");
        db.Countries.Add(country);
        db.CountryBrandings.Add(branding);
        await db.SaveChangesAsync();
        var sut = new AdminConfigurationService(db);

        var result = await sut.UpdateCountryBrandingAsync(
            "CI",
            ValidBrandingRequest("wele"),
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "unit-tests", "branding-update"),
            CancellationToken.None);

        Assert.Equal(AdminConfigurationUpdateStatus.Ok, result.Status);
        var savedBranding = await db.CountryBrandings.SingleAsync();
        Assert.Equal("wele", savedBranding.BrandName);
        Assert.Equal("Nouveau titre", savedBranding.HeroTitle);
        var audit = await db.AuditLogEntries.SingleAsync();
        Assert.Equal("AdminCountryBrandingUpdated", audit.Action);
        Assert.Equal(savedBranding.Id, audit.EntityId);
        Assert.Equal("branding-update", audit.CorrelationId);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }

    private static UpdateCountryBrandingRequest ValidBrandingRequest(string brandName)
        => new(
            brandName,
            "#0a66c2",
            "#ffffff",
            "#111111",
            "Nouveau titre",
            "Nouveau sous titre",
            "https://cdn.wele.ci/hero.jpg",
            "apple");
}
