using HomeService.Application.ProviderPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class ProviderMobileProfileUpdateServiceTests
{
    [Fact]
    public async Task AddDocumentAsync_WhenProviderExists_AddsNewMobileDocument()
    {
        await using var db = CreateDbContext();
        var provider = CreateProvider();
        db.Providers.Add(provider);
        await db.SaveChangesAsync();
        var sut = new ProviderMobileProfileUpdateService(db);

        var result = await sut.AddDocumentAsync(
            provider.Id,
            ProviderDocumentType.IdentityDocument,
            "cni.jpg",
            "providers/mobile/provider/cni.jpg",
            "image/jpeg",
            CancellationToken.None);
        await db.SaveChangesAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.Equal("IdentityDocument", result.Response.Type);
        Assert.Contains("/api/provider-portal/mobile/profile/documents/", result.Response.PreviewUrl);
        Assert.Single(await db.ProviderDocuments.Where(document => document.ProviderId == provider.Id).ToListAsync());
    }

    [Fact]
    public async Task AddPortfolioItemAsync_WhenServiceIsActive_AddsPendingPortfolioPhoto()
    {
        await using var db = CreateDbContext();
        var service = new Service("Coiffure", "Book requis", null);
        service.UpdateAssignmentRequirements(
            requiresPortfolio: true,
            minimumPortfolioItems: 3,
            requiresCompletionPhoto: false,
            requiresBeforeAfterPhotos: false,
            requiresDiploma: false,
            requiresAdminApprovalBeforeAssignment: false);
        var provider = CreateProvider();
        provider.SyncCompanyServices([(service.Id, ExperienceLevel.Confirmed, 5, ProviderServicePriceTier.Premium)]);
        db.Services.Add(service);
        db.Providers.Add(provider);
        await db.SaveChangesAsync();
        var sut = new ProviderMobileProfileUpdateService(db);

        var result = await sut.AddPortfolioItemAsync(
            provider.Id,
            service.Id,
            "tresse.jpg",
            "providers/mobile/provider/portfolio/tresse.jpg",
            "image/jpeg",
            CancellationToken.None);
        await db.SaveChangesAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.Equal("Pending", result.Response.Status);
        Assert.Equal(service.Id, result.Response.ServiceId);
        Assert.Single(await db.ProviderServicePortfolioItems.Where(item => item.ProviderId == provider.Id).ToListAsync());
    }

    [Fact]
    public async Task AddPortfolioItemAsync_WhenProviderDoesNotHaveService_ReturnsNotFound()
    {
        await using var db = CreateDbContext();
        var provider = CreateProvider();
        db.Providers.Add(provider);
        await db.SaveChangesAsync();
        var sut = new ProviderMobileProfileUpdateService(db);

        var result = await sut.AddPortfolioItemAsync(
            provider.Id,
            Guid.NewGuid(),
            "photo.jpg",
            "providers/mobile/provider/portfolio/photo.jpg",
            "image/jpeg",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Service prestataire introuvable ou inactif.", result.Message);
        Assert.Empty(await db.ProviderServicePortfolioItems.ToListAsync());
    }

    private static ProviderProfile CreateProvider()
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            "Awa",
            "Konate",
            "+2250701020304",
            "awa@wele.ci",
            new DateOnly(1995, 1, 12),
            "Cocody",
            ProviderGender.Female,
            ProviderEmploymentType.CompanyEmployee,
            4,
            5.348850m,
            -4.003150m,
            5);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }
}
