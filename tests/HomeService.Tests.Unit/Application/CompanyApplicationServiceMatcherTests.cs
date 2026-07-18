using HomeService.Application.Companies;

namespace HomeService.Tests.Unit.Application;

public sealed class CompanyApplicationServiceMatcherTests
{
    [Fact]
    public void FindBestCandidate_ReturnsPrestation_WhenRawNameMatchesExistingPrestation()
    {
        var serviceId = Guid.NewGuid();
        var prestationId = Guid.NewGuid();
        var catalog = new[]
        {
            new CompanyApplicationServiceCatalogItem(
                serviceId,
                "Blanchisserie",
                "blanchisserie",
                prestationId,
                "Repassage",
                "repassage")
        };

        var candidate = CompanyApplicationServiceMatcher.FindBestCandidate(" repassage ", catalog);

        Assert.NotNull(candidate);
        Assert.Equal(serviceId, candidate.ServiceId);
        Assert.Equal(prestationId, candidate.ServicePrestationId);
        Assert.Equal("Prestation", candidate.Kind);
        Assert.Equal(100, candidate.Score);
    }

    [Fact]
    public void FindBestCandidate_ReturnsPrestation_WhenRawNameUsesServicePrestationLabelWithDash()
    {
        var serviceId = Guid.NewGuid();
        var prestationId = Guid.NewGuid();
        var catalog = new[]
        {
            new CompanyApplicationServiceCatalogItem(
                serviceId,
                "Blanchisserie",
                "blanchisserie",
                prestationId,
                "Repassage",
                "repassage")
        };

        var candidate = CompanyApplicationServiceMatcher.FindBestCandidate("Blanchisserie - Repassage", catalog);

        Assert.NotNull(candidate);
        Assert.Equal(serviceId, candidate.ServiceId);
        Assert.Equal(prestationId, candidate.ServicePrestationId);
        Assert.Equal("Prestation", candidate.Kind);
    }

    [Fact]
    public void FindBestCandidate_IsAccentAndCaseTolerant()
    {
        var serviceId = Guid.NewGuid();
        var catalog = new[]
        {
            new CompanyApplicationServiceCatalogItem(
                serviceId,
                "Ménage à domicile",
                "menage a domicile",
                null,
                null,
                null)
        };

        var candidate = CompanyApplicationServiceMatcher.FindBestCandidate("MENAGE A DOMICILE", catalog);

        Assert.NotNull(candidate);
        Assert.Equal(serviceId, candidate.ServiceId);
        Assert.Null(candidate.ServicePrestationId);
    }

    [Fact]
    public void FindBestCandidate_ReturnsNull_WhenOnlyWeakMatchExists()
    {
        var catalog = new[]
        {
            new CompanyApplicationServiceCatalogItem(
                Guid.NewGuid(),
                "Blanchisserie",
                "blanchisserie",
                null,
                null,
                null)
        };

        var candidate = CompanyApplicationServiceMatcher.FindBestCandidate("depannage auto", catalog);

        Assert.Null(candidate);
    }
}
