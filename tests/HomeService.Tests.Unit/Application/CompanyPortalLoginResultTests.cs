using HomeService.Application.CompanyPortal;

namespace HomeService.Tests.Unit.Application;

public sealed class CompanyPortalLoginResultTests
{
    [Fact]
    public void MissingCredentials_ReturnsBusinessMessage()
    {
        var result = CompanyPortalLoginResult.MissingCredentials();

        Assert.Equal(CompanyPortalLoginStatus.MissingCredentials, result.Status);
        Assert.Equal("Email et mot de passe sont obligatoires.", result.Message);
    }

    [Fact]
    public void Suspended_ReturnsBusinessMessage()
    {
        var result = CompanyPortalLoginResult.Suspended();

        Assert.Equal(CompanyPortalLoginStatus.CompanySuspended, result.Status);
        Assert.Equal("Cette entreprise est suspendue. Contactez le support.", result.Message);
    }
}
