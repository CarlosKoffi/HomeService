using HomeService.Application.CompanyPortal;

namespace HomeService.Tests.Unit.Application;

public sealed class CompanyProviderValidatorTests
{
    [Fact]
    public void Validate_WhenProviderIsValid_ReturnsNoErrors()
    {
        var provider = ValidProvider();

        var errors = CompanyProviderValidator.Validate(provider);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(16, false)]
    [InlineData(15, true)]
    public void Validate_EnforcesMinimumAge(int age, bool expectError)
    {
        var dateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-age));
        var provider = ValidProvider(dateOfBirth: dateOfBirth);

        var errors = CompanyProviderValidator.Validate(provider);

        Assert.Equal(expectError, errors.Any(error => error.Contains("16 ans", StringComparison.OrdinalIgnoreCase)));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(60, false)]
    [InlineData(-1, true)]
    [InlineData(61, true)]
    public void Validate_EnforcesExperienceBounds(int yearsOfExperience, bool expectError)
    {
        var provider = ValidProvider(yearsOfExperience: yearsOfExperience);

        var errors = CompanyProviderValidator.Validate(provider);

        Assert.Equal(expectError, errors.Any(error => error.Contains("experience", StringComparison.OrdinalIgnoreCase)));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(100, false)]
    [InlineData(0, true)]
    [InlineData(101, true)]
    public void Validate_EnforcesMissionRadiusBounds(int radiusKm, bool expectError)
    {
        var provider = ValidProvider(radiusKm: radiusKm);

        var errors = CompanyProviderValidator.Validate(provider);

        Assert.Equal(expectError, errors.Any(error => error.Contains("perimetre", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Validate_WhenPhotoAndIdentityAreMissing_ReturnsBothErrors()
    {
        var provider = ValidProvider(hasPhoto: false, hasIdentityDocument: false);

        var errors = CompanyProviderValidator.Validate(provider);

        Assert.Contains(errors, error => error.Contains("photo", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("identite", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_WhenServicesAreMissing_ReturnsServiceError()
    {
        var provider = ValidProvider(serviceIds: []);

        var errors = CompanyProviderValidator.Validate(provider);

        Assert.Contains(errors, error => error.Contains("service", StringComparison.OrdinalIgnoreCase));
    }

    private static CompanyProviderFormData ValidProvider(
        DateOnly? dateOfBirth = null,
        int yearsOfExperience = 4,
        int radiusKm = 5,
        IReadOnlyList<Guid>? serviceIds = null,
        bool hasPhoto = true,
        bool hasIdentityDocument = true)
    {
        return new CompanyProviderFormData(
            "Awa",
            "Kone",
            "+2250701020304",
            dateOfBirth ?? DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-30)),
            "Cocody",
            yearsOfExperience,
            radiusKm,
            serviceIds ?? [Guid.NewGuid()],
            hasPhoto,
            hasIdentityDocument);
    }
}
