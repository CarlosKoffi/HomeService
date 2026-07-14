using HomeService.Application.Admin;
using HomeService.Contracts.Branding;
using HomeService.Domain.Entities;

namespace HomeService.Tests.Unit.Application;

public sealed class AdminConfigurationResultTests
{
    [Fact]
    public void CountryBrandingValidationFailed_CarriesMessage()
    {
        var result = AdminCountryBrandingUpdateResult.ValidationFailed("Couleur invalide.");

        Assert.Equal(AdminConfigurationUpdateStatus.ValidationFailed, result.Status);
        Assert.Equal("Couleur invalide.", result.Message);
        Assert.Null(result.Branding);
        Assert.Null(result.Response);
    }

    [Fact]
    public void CountryBrandingNotFound_CarriesBusinessMessage()
    {
        var result = AdminCountryBrandingUpdateResult.CountryNotFound();

        Assert.Equal(AdminConfigurationUpdateStatus.NotFound, result.Status);
        Assert.Equal("Pays introuvable.", result.Message);
    }

    [Fact]
    public void CountryBrandingOk_CarriesBrandingAndResponse()
    {
        var countryId = Guid.NewGuid();
        var branding = new CountryBranding(
            countryId,
            "Kaza",
            "#0a66c2",
            "#ffffff",
            "#111111",
            "Titre",
            "Sous titre",
            null,
            "apple");
        var response = new CountryBrandingResponse("CI", "Cote d'Ivoire", "Kaza", "#0a66c2", "#ffffff", "#111111", "Titre", "Sous titre", null, "apple");

        var result = AdminCountryBrandingUpdateResult.Ok(branding, null, response, response);

        Assert.Equal(AdminConfigurationUpdateStatus.Ok, result.Status);
        Assert.Same(branding, result.Branding);
        Assert.Same(response, result.Response);
    }

    [Fact]
    public void AssignmentModeValidationFailed_CarriesMessage()
    {
        var result = AdminCompanyAssignmentModeUpdateResult.ValidationFailed("Mode invalide.");

        Assert.Equal(AdminConfigurationUpdateStatus.ValidationFailed, result.Status);
        Assert.Equal("Mode invalide.", result.Message);
        Assert.Null(result.Company);
    }
}
