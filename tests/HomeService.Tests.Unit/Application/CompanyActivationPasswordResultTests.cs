using HomeService.Application.Companies;
using HomeService.Contracts.Companies;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Tests.Unit.Application;

public sealed class CompanyActivationPasswordResultTests
{
    [Fact]
    public void Ok_CarriesApplicationCompanyAndPreviousStatus()
    {
        var application = CreateApplication();
        var company = new Company("CI Home Service", "+2250700000000", "direction@entreprise.ci");
        var response = new CompanyActivationPasswordResponse(true, "Pret.");

        var result = CompanyActivationPasswordResult.Ok(
            response,
            application,
            company,
            CompanyApplicationStatus.ActivationSent,
            "direction@entreprise.ci");

        Assert.Equal(CompanyActivationPasswordStatus.Ok, result.Status);
        Assert.Same(response, result.Response);
        Assert.Same(application, result.Application);
        Assert.Same(company, result.Company);
        Assert.Equal(CompanyApplicationStatus.ActivationSent, result.PreviousStatus);
        Assert.Equal("direction@entreprise.ci", result.Email);
    }

    [Fact]
    public void InvalidOrExpiredToken_ReturnsBusinessMessage()
    {
        var result = CompanyActivationPasswordResult.InvalidOrExpiredToken();

        Assert.Equal(CompanyActivationPasswordStatus.InvalidOrExpiredToken, result.Status);
        Assert.Equal("Le lien d'activation est invalide ou expire.", result.Message);
    }

    [Fact]
    public void DuplicatePortalUser_ReturnsBusinessMessage()
    {
        var result = CompanyActivationPasswordResult.DuplicatePortalUser();

        Assert.Equal(CompanyActivationPasswordStatus.DuplicatePortalUser, result.Status);
        Assert.Equal("Un compte portail existe deja pour cet email.", result.Message);
    }

    private static CompanyApplication CreateApplication()
    {
        return new CompanyApplication(
            "CI Home Service",
            null,
            "Abidjan",
            null,
            "John PriPri",
            "direction@entreprise.ci",
            "+2250700000000",
            "Menage",
            12);
    }
}
