using HomeService.Application.Cms;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class ClientHomeCmsQueryServiceTests
{
    [Fact]
    public async Task GetAsync_WhenClientContentExists_ReturnsCmsValueAndSectionFallbacks()
    {
        await using var db = CreateDbContext();
        var country = new Country("CI", "Côte d'Ivoire", "XOF", true);
        var language = new Language("fr", "Français", true);
        var site = new CmsSite("client-public", "Wélé clients", CmsSiteSurface.PublicClient, country.Id, language.Id);
        var page = new CmsPage(site.Id, "home", "Accueil clients", "landing");
        var version = new CmsPageVersion(page.Id, 1);
        var heroDefinition = new CmsComponentDefinition("HeroStandard", "Hero standard", 1);
        var heroSection = new CmsSection(version.Id, heroDefinition.Id, "Accueil - Hero", "main", 1);
        var headline = new CmsContentValue(heroSection.Id, "headline", CmsContentValueType.ShortText, language.Id);
        headline.SetText("Un titre piloté depuis le CMS");

        db.AddRange(country, language, site, page, version, heroDefinition, heroSection, headline);
        await db.SaveChangesAsync();

        var response = await new ClientHomeCmsQueryService(db).GetAsync("fr", "CI", CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal("Un titre piloté depuis le CMS", response.Hero.Headline);
        Assert.Equal("Tout ce que Wélé peut faire pour vous.", response.Services.Headline);
        Assert.NotEmpty(response.Steps.Items);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase($"client-home-cms-{Guid.NewGuid():N}")
            .Options;

        return new HomeServiceDbContext(options);
    }
}
