using HomeService.Application.Companies;
using HomeService.Contracts.Companies;

namespace HomeService.Tests.Unit.Application;

public sealed class CompanyActivationPreviewResultTests
{
    [Fact]
    public void Ok_CarriesPreviewResponse()
    {
        var response = new CompanyActivationPreviewResponse(
            Guid.NewGuid(),
            "CI Home Service",
            "direction@entreprise.ci",
            DateTimeOffset.UtcNow.AddHours(24));

        var result = CompanyActivationPreviewResult.Ok(response);

        Assert.Equal(CompanyActivationPreviewStatus.Ok, result.Status);
        Assert.Same(response, result.Response);
        Assert.Null(result.Message);
    }

    [Fact]
    public void InvalidOrExpiredToken_ReturnsBusinessMessage()
    {
        var result = CompanyActivationPreviewResult.InvalidOrExpiredToken();

        Assert.Equal(CompanyActivationPreviewStatus.InvalidOrExpiredToken, result.Status);
        Assert.Equal("Ce lien d'activation est invalide ou expire.", result.Message);
    }
}
