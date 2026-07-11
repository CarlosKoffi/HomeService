using HomeService.Application.CompanyPortal;
using HomeService.Contracts.CompanyPortal;

namespace HomeService.Tests.Unit.Application;

public sealed class CompanyPortalAuthServiceTests
{
    [Theory]
    [InlineData("", "Password123")]
    [InlineData("direction@entreprise.ci", "")]
    [InlineData("   ", "   ")]
    public async Task LoginAsync_WhenCredentialsMissing_ReturnsMissingCredentials(string email, string password)
    {
        var service = new CompanyPortalAuthService(null!);

        var result = await service.LoginAsync(new CompanyPortalLoginRequest(email, password, false), CancellationToken.None);

        Assert.Equal(CompanyPortalLoginStatus.MissingCredentials, result.Status);
        Assert.Equal("Email et mot de passe sont obligatoires.", result.Message);
    }
}
