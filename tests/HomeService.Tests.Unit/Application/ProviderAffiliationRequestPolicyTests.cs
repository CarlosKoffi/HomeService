using HomeService.Application.ProviderPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Tests.Unit.Application;

public sealed class ProviderAffiliationRequestPolicyTests
{
    [Fact]
    public void EvaluateProvider_ReturnsNotFound_WhenProviderDoesNotExist()
    {
        var result = ProviderAffiliationRequestPolicy.EvaluateProvider(null);

        Assert.Equal(ProviderAffiliationRequestStatusCode.NotFound, result.Status);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void EvaluateProvider_ReturnsValidationFailed_WhenProviderIsAlreadyAttachedToCompany()
    {
        var provider = new ProviderProfile(
            Guid.NewGuid(),
            "Awa",
            "Kone",
            "0700000000",
            "awa.kone@kaza.ci",
            new DateOnly(1995, 1, 1),
            "Cocody",
            ProviderGender.Female,
            ProviderEmploymentType.CompanyEmployee,
            4,
            null,
            null,
            5);

        var result = ProviderAffiliationRequestPolicy.EvaluateProvider(provider);

        Assert.Equal(ProviderAffiliationRequestStatusCode.ValidationFailed, result.Status);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void EvaluateProvider_ReturnsSuccess_WhenProviderIsInterimCandidate()
    {
        var provider = new ProviderProfile(
            "Awa",
            "Kone",
            "0700000000",
            "awa.kone@kaza.ci",
            new DateOnly(1995, 1, 1),
            "Cocody",
            ProviderGender.Female,
            4,
            null,
            null,
            5);

        var result = ProviderAffiliationRequestPolicy.EvaluateProvider(provider);

        Assert.Equal(ProviderAffiliationRequestStatusCode.Success, result.Status);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void EvaluateCompany_ReturnsNotFound_WhenCompanyDoesNotExistOrIsInactive()
    {
        var result = ProviderAffiliationRequestPolicy.EvaluateCompany(null, hasPendingRequest: false);

        Assert.Equal(ProviderAffiliationRequestStatusCode.NotFound, result.Status);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void EvaluateCompany_ReturnsValidationFailed_WhenRequestAlreadyPending()
    {
        var company = new Company("Kaza Partner", "0100000000", "contact@kaza.ci");
        company.Approve();
        company.SetInterimApplications(true);

        var result = ProviderAffiliationRequestPolicy.EvaluateCompany(company, hasPendingRequest: true);

        Assert.Equal(ProviderAffiliationRequestStatusCode.ValidationFailed, result.Status);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void EvaluateCompany_ReturnsValidationFailed_WhenCompanyDoesNotAcceptInterim()
    {
        var company = new Company("Kaza Partner", "0100000000", "contact@kaza.ci");
        company.Approve();

        var result = ProviderAffiliationRequestPolicy.EvaluateCompany(company, hasPendingRequest: false);

        Assert.Equal(ProviderAffiliationRequestStatusCode.ValidationFailed, result.Status);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void EvaluateCompany_ReturnsSuccess_WhenCompanyExistsAndNoPendingRequest()
    {
        var company = new Company("Kaza Partner", "0100000000", "contact@kaza.ci");
        company.Approve();
        company.SetInterimApplications(true);

        var result = ProviderAffiliationRequestPolicy.EvaluateCompany(company, hasPendingRequest: false);

        Assert.Equal(ProviderAffiliationRequestStatusCode.Success, result.Status);
        Assert.True(result.IsSuccess);
    }
}
