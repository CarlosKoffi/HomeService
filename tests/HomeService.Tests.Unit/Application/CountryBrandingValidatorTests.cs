using HomeService.Application.Branding;
using HomeService.Contracts.Branding;

namespace HomeService.Tests.Unit.Application;

public sealed class CountryBrandingValidatorTests
{
    [Fact]
    public void Validate_WhenBrandingIsValid_ReturnsNoError()
    {
        var request = ValidRequest();

        var error = CountryBrandingValidator.Validate(request);

        Assert.Null(error);
    }

    [Theory]
    [InlineData("#008753", true)]
    [InlineData("#FF8200", true)]
    [InlineData("008753", false)]
    [InlineData("#12345", false)]
    [InlineData("#GGGGGG", false)]
    public void IsHexColor_ValidatesExpectedFormat(string value, bool expected)
    {
        Assert.Equal(expected, CountryBrandingValidator.IsHexColor(value));
    }

    [Theory]
    [InlineData("ftp://cdn.test/image.jpg", "image")]
    [InlineData("not-a-url", "image")]
    public void Validate_WhenHeroImageUrlIsInvalid_ReturnsError(string url, string expectedFragment)
    {
        var request = ValidRequest(heroImageUrl: url);

        var error = CountryBrandingValidator.Validate(request);

        Assert.NotNull(error);
        Assert.Contains(expectedFragment, error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_WhenBrandNameIsMissing_ReturnsError()
    {
        var request = ValidRequest(brandName: "");

        var error = CountryBrandingValidator.Validate(request);

        Assert.NotNull(error);
        Assert.Contains("marque", error, StringComparison.OrdinalIgnoreCase);
    }

    private static UpdateCountryBrandingRequest ValidRequest(string brandName = "Kaza", string? heroImageUrl = "https://cdn.kaza.ci/hero.jpg")
    {
        return new UpdateCountryBrandingRequest(
            brandName,
            "#008753",
            "#ffffff",
            "#ff8200",
            "Le service a domicile en confiance",
            "Des entreprises verifiees, des prestataires suivis.",
            heroImageUrl,
            "ci-lines");
    }
}
