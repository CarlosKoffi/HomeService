using HomeService.Application.Admin;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Tests.Unit.Application;

public sealed class AdminCompanyApplicationReviewResultTests
{
    [Fact]
    public void Ok_CarriesApplicationAndPreviousStatus()
    {
        var application = CreateApplication();

        var result = AdminCompanyApplicationReviewResult.Ok(application, CompanyApplicationStatus.Submitted);

        Assert.Equal(AdminCompanyApplicationReviewStatus.Ok, result.Status);
        Assert.Same(application, result.Application);
        Assert.Equal(CompanyApplicationStatus.Submitted, result.PreviousStatus);
    }

    [Fact]
    public void ValidationFailed_CarriesMessage()
    {
        var result = AdminCompanyApplicationReviewResult.ValidationFailed("Une note est obligatoire.");

        Assert.Equal(AdminCompanyApplicationReviewStatus.ValidationFailed, result.Status);
        Assert.Equal("Une note est obligatoire.", result.Message);
        Assert.Null(result.Application);
    }

    [Fact]
    public void MissingRequiredDocuments_CarriesBusinessMessage()
    {
        var result = AdminCompanyApplicationReviewResult.MissingRequiredDocuments("Documents requis.");

        Assert.Equal(AdminCompanyApplicationReviewStatus.MissingRequiredApprovedDocuments, result.Status);
        Assert.Equal("Documents requis.", result.Message);
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
