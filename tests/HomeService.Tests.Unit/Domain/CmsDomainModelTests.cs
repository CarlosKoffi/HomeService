using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Tests.Unit.Domain;

public sealed class CmsDomainModelTests
{
    [Fact]
    public void CmsSite_NormalizesCodeAndCanActivate()
    {
        var site = new CmsSite(" Company-Public ", "Kaza entreprises", CmsSiteSurface.PublicCompany, Guid.NewGuid(), Guid.NewGuid());

        site.Activate();
        site.SetHomePage(" Home ");

        Assert.Equal("company-public", site.Code);
        Assert.Equal("home", site.HomePageCode);
        Assert.Equal(CmsSiteStatus.Active, site.Status);
        Assert.NotNull(site.UpdatedAt);
    }

    [Fact]
    public void CmsPageVersion_WhenVersionNumberIsZero_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CmsPageVersion(Guid.NewGuid(), 0));
    }

    [Fact]
    public void CmsSection_WhenPositionIsNegative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CmsSection(Guid.NewGuid(), Guid.NewGuid(), "Hero", "main", -1));
    }

    [Fact]
    public void CmsContentValue_SetText_TrimsValue()
    {
        var value = new CmsContentValue(Guid.NewGuid(), "headline", CmsContentValueType.ShortText, Guid.NewGuid());

        value.SetText("  Developpez votre activite  ");

        Assert.Equal("Developpez votre activite", value.TextValue);
        Assert.NotNull(value.UpdatedAt);
    }

    [Fact]
    public void CmsMediaAsset_MarkAvailable_UpdatesStatus()
    {
        var asset = new CmsMediaAsset("hero.jpg", "cms/company/hero.jpg", "image/jpeg", 120_000);

        asset.MarkAvailable();

        Assert.Equal(CmsMediaStatus.Available, asset.Status);
        Assert.NotNull(asset.UpdatedAt);
    }
}
