using HomeService.Application.Companies;
using HomeService.Contracts.Companies;

namespace HomeService.Tests.Unit.Application;

public sealed class CompanyActivationPasswordValidatorTests
{
    [Fact]
    public void Validate_WhenPasswordIsStrong_ReturnsNoError()
    {
        var request = new CompanyActivationPasswordRequest("activation-token", "Password123", "Password123");

        var error = CompanyActivationPasswordValidator.Validate(request);

        Assert.Null(error);
    }

    [Theory]
    [InlineData("", "Password123", "Password123", "token")]
    [InlineData("token", "short", "short", "10 caracteres")]
    [InlineData("token", "passwordpassword", "passwordpassword", "majuscule")]
    [InlineData("token", "Password123", "Password124", "correspondent")]
    public void Validate_WhenRequestIsInvalid_ReturnsSpecificError(string token, string password, string confirmPassword, string expectedFragment)
    {
        var request = new CompanyActivationPasswordRequest(token, password, confirmPassword);

        var error = CompanyActivationPasswordValidator.Validate(request);

        Assert.NotNull(error);
        Assert.Contains(expectedFragment, error, StringComparison.OrdinalIgnoreCase);
    }
}
