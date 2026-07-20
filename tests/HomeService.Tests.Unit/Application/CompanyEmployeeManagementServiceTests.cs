using HomeService.Application.CompanyPortal;
using HomeService.Contracts.CompanyPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class CompanyEmployeeManagementServiceTests
{
    [Fact]
    public async Task UpdateServicesAsync_AddsServiceAndPrestationForCompanyEmployee()
    {
        await using var db = CreateDbContext();
        var companyId = Guid.NewGuid();
        var provider = CreateProvider(companyId);
        var service = new Service("Blanchisserie", null, null);
        var prestation = service.AddPrestation("Repassage", null, 1, 2500, 4500, "XOF");
        db.Providers.Add(provider);
        db.Services.Add(service);
        await db.SaveChangesAsync();

        var result = await new CompanyEmployeeManagementService(db).UpdateServicesAsync(
            companyId,
            provider.Id,
            new UpdateCompanyEmployeeServicesRequest([
                new UpsertCompanyEmployeeServiceRequest(
                    service.Id,
                    nameof(ExperienceLevel.Confirmed),
                    6,
                    nameof(ProviderServicePriceTier.Normal),
                    [prestation.Id])
            ]),
            CancellationToken.None);
        await db.SaveChangesAsync();

        Assert.Equal(CompanyEmployeeOperationStatus.Ok, result.Status);
        var providerService = await db.ProviderServices
            .Include(item => item.Prestations)
            .SingleAsync(item => item.ProviderId == provider.Id && item.ServiceId == service.Id);
        Assert.True(providerService.IsActive);
        Assert.Equal(ExperienceLevel.Confirmed, providerService.ExperienceLevel);
        Assert.Equal(6, providerService.YearsOfExperience);
        var providerPrestation = Assert.Single(providerService.Prestations);
        Assert.Equal(prestation.Id, providerPrestation.ServicePrestationId);
        Assert.True(providerPrestation.IsActive);
    }

    [Fact]
    public async Task UpdateServicesAsync_DeactivatesRemovedPrestationWithoutDroppingService()
    {
        await using var db = CreateDbContext();
        var companyId = Guid.NewGuid();
        var provider = CreateProvider(companyId);
        var service = new Service("Jardinage", null, null);
        var hedge = service.AddPrestation("Tailler une haie", null, 1, 5000, 9000, "XOF");
        var lawn = service.AddPrestation("Tondre le gazon", null, 2, 4000, 8000, "XOF");
        provider.SyncCompanyServices([(service.Id, ExperienceLevel.Confirmed, 4, ProviderServicePriceTier.Normal)]);
        provider.Services.Single().SyncPrestations([hedge.Id, lawn.Id]);
        db.Providers.Add(provider);
        db.Services.Add(service);
        await db.SaveChangesAsync();

        var result = await new CompanyEmployeeManagementService(db).UpdateServicesAsync(
            companyId,
            provider.Id,
            new UpdateCompanyEmployeeServicesRequest([
                new UpsertCompanyEmployeeServiceRequest(
                    service.Id,
                    nameof(ExperienceLevel.Expert),
                    8,
                    nameof(ProviderServicePriceTier.Premium),
                    [lawn.Id])
            ]),
            CancellationToken.None);
        await db.SaveChangesAsync();

        Assert.Equal(CompanyEmployeeOperationStatus.Ok, result.Status);
        var providerService = await db.ProviderServices
            .Include(item => item.Prestations)
            .SingleAsync(item => item.ProviderId == provider.Id && item.ServiceId == service.Id);
        Assert.True(providerService.IsActive);
        Assert.Equal(ExperienceLevel.Expert, providerService.ExperienceLevel);
        Assert.Contains(providerService.Prestations, item => item.ServicePrestationId == hedge.Id && !item.IsActive);
        Assert.Contains(providerService.Prestations, item => item.ServicePrestationId == lawn.Id && item.IsActive);
    }

    [Fact]
    public async Task ReplaceDocumentAsync_AddsNewDocumentVersionWithoutRemovingPreviousFile()
    {
        await using var db = CreateDbContext();
        var companyId = Guid.NewGuid();
        var provider = CreateProvider(companyId);
        var existingDocument = new ProviderDocument(
            provider.Id,
            ProviderDocumentType.IdentityDocument,
            "old-id.png",
            "providers/company/provider/old-id.png",
            "image/png");
        db.Providers.Add(provider);
        db.ProviderDocuments.Add(existingDocument);
        await db.SaveChangesAsync();

        var result = await new CompanyEmployeeManagementService(db).ReplaceDocumentAsync(
            companyId,
            provider.Id,
            ProviderDocumentType.IdentityDocument,
            "new-id.png",
            "providers/company/provider/new-id.png",
            "image/png",
            CancellationToken.None);
        await db.SaveChangesAsync();

        Assert.Equal(CompanyEmployeeOperationStatus.Ok, result.Status);
        Assert.Empty(result.ReplacedStoragePaths);
        var documents = await db.ProviderDocuments
            .Where(document => document.ProviderId == provider.Id && document.DocumentType == ProviderDocumentType.IdentityDocument)
            .OrderBy(document => document.CreatedAt)
            .ToListAsync();
        Assert.Equal(2, documents.Count);
        Assert.Contains(documents, document => document.StoragePath == "providers/company/provider/old-id.png");
        Assert.Contains(documents, document => document.StoragePath == "providers/company/provider/new-id.png");
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }

    private static ProviderProfile CreateProvider(Guid companyId)
    {
        return new ProviderProfile(
            companyId,
            "Awa",
            "Konate",
            "+2250700000000",
            "awa.konate@example.ci",
            new DateOnly(1995, 4, 12),
            "Cocody",
            ProviderGender.Female,
            ProviderEmploymentType.CompanyEmployee,
            4,
            5.348850m,
            -4.003150m,
            5);
    }
}
