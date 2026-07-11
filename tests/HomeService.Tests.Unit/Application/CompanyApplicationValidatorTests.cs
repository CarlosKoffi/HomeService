using HomeService.Application.Companies;
using HomeService.Contracts.Companies;

namespace HomeService.Tests.Unit.Application;

public sealed class CompanyApplicationValidatorTests
{
    [Fact]
    public void Validate_WhenRequestIsValid_ReturnsNoErrors()
    {
        var request = ValidRequest();

        var errors = CompanyApplicationValidator.Validate(request);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_WhenRequiredFieldsAreInvalid_ReturnsExpectedErrors()
    {
        var request = new RegisterCompanyRequest(
            CompanyName: "CI",
            RegistrationNumber: "1",
            City: "",
            Address: null,
            ContactName: "A",
            Email: "bad-email",
            PhoneNumber: "123",
            Password: "short",
            ConfirmPassword: "different",
            Services: [],
            EstimatedProviderCount: 0);

        var errors = CompanyApplicationValidator.Validate(request);

        Assert.Contains(errors, error => error.Contains("nom legal", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("numero legal", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("ville", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("responsable", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("email", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("telephone", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("prestataires", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("service", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("mot de passe", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("correspondent", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(10000)]
    public void Validate_WhenProviderCountIsInsideBounds_ReturnsNoProviderCountError(int? providerCount)
    {
        var request = ValidRequest(providerCount);

        var errors = CompanyApplicationValidator.Validate(request);

        Assert.DoesNotContain(errors, error => error.Contains("prestataires", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10001)]
    public void Validate_WhenProviderCountIsOutsideBounds_ReturnsProviderCountError(int providerCount)
    {
        var request = ValidRequest(providerCount);

        var errors = CompanyApplicationValidator.Validate(request);

        Assert.Contains(errors, error => error.Contains("prestataires", StringComparison.OrdinalIgnoreCase));
    }

    private static RegisterCompanyRequest ValidRequest(int? providerCount = 12)
    {
        return new RegisterCompanyRequest(
            "CI Home Service",
            null,
            "Abidjan",
            "Cocody",
            "John Pripri",
            "direction@entreprise.ci",
            "+2250701020304",
            "Password123",
            "Password123",
            ["Menage", "Jardinage"],
            providerCount);
    }
}
