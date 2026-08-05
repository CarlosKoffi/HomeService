using HomeService.Application.ProviderPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class ProviderMobileProfileServiceTests
{
    [Fact]
    public async Task GetAsync_ForCompanyEmployee_MasksPricesAndReturnsProfessionalProfile()
    {
        await using var db = CreateDbContext();
        var company = new Company("Wele Services", "+2250701111111", "ops@wele.ci");
        company.Approve();
        var service = new Service("Blanchisserie", "Linge et repassage", null);
        service.UpdateAssignmentRequirements(
            requiresPortfolio: true,
            minimumPortfolioItems: 1,
            requiresCompletionPhoto: false,
            requiresBeforeAfterPhotos: false,
            requiresDiploma: false,
            requiresAdminApprovalBeforeAssignment: false);
        var prestation = service.AddPrestation("Repassage", null, 1, 2_500, 4_500);
        var provider = new ProviderProfile(
            company.Id,
            "Awa",
            "Konate",
            "+2250702222222",
            "awa@wele.ci",
            new DateOnly(1994, 2, 3),
            "Cocody",
            ProviderGender.Female,
            ProviderEmploymentType.CompanyEmployee,
            5,
            5.348850m,
            -4.003150m,
            5);
        provider.SyncCompanyServices([
            (service.Id, ExperienceLevel.Confirmed, 5, ProviderServicePriceTier.Premium)
        ]);
        provider.Services.Single().SyncPrestations([prestation.Id]);
        provider.AttachDocument(new ProviderDocument(
            provider.Id,
            ProviderDocumentType.Photo,
            "awa.jpg",
            "providers/awa/photo.jpg",
            "image/jpeg"));
        provider.AttachDocument(new ProviderDocument(
            provider.Id,
            ProviderDocumentType.IdentityDocument,
            "cni.pdf",
            "providers/awa/cni.pdf",
            "application/pdf"));
        provider.Approve();
        var portfolioItem = new ProviderServicePortfolioItem(
            provider.Id,
            service.Id,
            "linge.jpg",
            "providers/awa/portfolio/linge.jpg",
            "image/jpeg",
            1);
        portfolioItem.Approve();

        db.Companies.Add(company);
        db.Services.Add(service);
        db.Providers.Add(provider);
        db.ProviderServicePortfolioItems.Add(portfolioItem);
        await db.SaveChangesAsync();
        var sut = new ProviderMobileProfileService(db);

        var result = await sut.GetAsync(provider.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.Equal("Awa Konate", result.Response.FullName);
        Assert.Equal("Wele Services", result.Response.CompanyName);
        Assert.False(result.Response.CanViewPrices);
        Assert.NotNull(result.Response.ProfilePhotoUrl);
        Assert.True(result.Response.IsApprovedForMissions);
        Assert.Null(result.Response.ProfileCompletion);
        Assert.Equal(2, result.Response.Documents.Count);
        var profileService = Assert.Single(result.Response.Services);
        Assert.Equal("Blanchisserie", profileService.ServiceName);
        Assert.True(profileService.RequiresPortfolio);
        Assert.Equal(1, profileService.PortfolioPhotoCount);
        Assert.True(profileService.CanReceiveMissions);
        Assert.Null(profileService.PriceTier);
        var profilePrestation = Assert.Single(profileService.Prestations);
        Assert.Equal("Repassage", profilePrestation.Name);
        Assert.Null(profilePrestation.PriceMinAmount);
        Assert.Null(profilePrestation.PriceMaxAmount);
        Assert.Single(result.Response.PortfolioItems);
    }

    [Fact]
    public async Task GetAsync_ForTemporaryWorker_AllowsPriceData()
    {
        await using var db = CreateDbContext();
        var company = new Company("Wele Services", "+2250701111111", "ops@wele.ci");
        company.Approve();
        var service = new Service("Plomberie", null, null);
        var prestation = service.AddPrestation("Debouchage", null, 1, 5_000, 8_000);
        var provider = new ProviderProfile(
            company.Id,
            "Malo",
            "Kone",
            "+2250702000000",
            "malo@wele.ci",
            new DateOnly(1994, 2, 3),
            "Cocody",
            ProviderGender.Male,
            ProviderEmploymentType.TemporaryWorker,
            5,
            5.348850m,
            -4.003150m,
            5);
        provider.SyncCompanyServices([(service.Id, ExperienceLevel.Expert, 5, ProviderServicePriceTier.Premium)]);
        provider.Services.Single().SyncPrestations([prestation.Id]);
        provider.Approve();
        db.Companies.Add(company);
        db.Services.Add(service);
        db.Providers.Add(provider);
        await db.SaveChangesAsync();

        var result = await new ProviderMobileProfileService(db).GetAsync(provider.Id, CancellationToken.None);

        Assert.True(result.Response!.CanViewPrices);
        var profileService = Assert.Single(result.Response.Services);
        Assert.Equal("Premium", profileService.PriceTier);
        var profilePrestation = Assert.Single(profileService.Prestations);
        Assert.Equal(5_000, profilePrestation.PriceMinAmount);
        Assert.Equal(8_000, profilePrestation.PriceMaxAmount);
    }

    [Fact]
    public async Task GetAsync_WhenProfileHasMissingRequirements_ReturnsCompletionItems()
    {
        await using var db = CreateDbContext();
        var company = new Company("Wele Services", "+2250701111111", "ops@wele.ci");
        var provider = new ProviderProfile(
            company.Id,
            "Malou",
            "Diallo",
            "+2250703333333",
            null,
            new DateOnly(1998, 5, 4),
            "Yopougon",
            ProviderGender.Male,
            ProviderEmploymentType.TemporaryWorker,
            2,
            null,
            null,
            5);
        db.Companies.Add(company);
        db.Providers.Add(provider);
        await db.SaveChangesAsync();
        var sut = new ProviderMobileProfileService(db);

        var result = await sut.GetAsync(provider.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response!.ProfileCompletion);
        Assert.Contains("Photo de profil", result.Response.ProfileCompletion!.MissingItems);
        Assert.Contains("Piece d'identite", result.Response.ProfileCompletion.MissingItems);
        Assert.Contains("Service actif", result.Response.ProfileCompletion.MissingItems);
        Assert.Contains("Zone de mission", result.Response.ProfileCompletion.MissingItems);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }
}
