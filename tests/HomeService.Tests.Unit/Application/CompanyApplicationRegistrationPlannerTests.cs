using HomeService.Application.Companies;
using HomeService.Contracts.Companies;

namespace HomeService.Tests.Unit.Application;

public sealed class CompanyApplicationRegistrationPlannerTests
{
    [Fact]
    public void Build_NormalizesEmailAndDistinctServices()
    {
        var request = CreateRequest(
            email: " Direction@Entreprise.CI ",
            services: ["Menage", " menage ", "Jardinage"]);

        var plan = CompanyApplicationRegistrationPlanner.Build(request);

        Assert.Empty(plan.ValidationErrors);
        Assert.Equal("direction@entreprise.ci", plan.Email);
        Assert.Equal(["Menage", "Jardinage"], plan.ServiceNames);
    }

    [Fact]
    public void Build_WhenRequestInvalid_ReturnsValidationErrors()
    {
        var request = CreateRequest(companyName: "", email: "bad", services: []);

        var plan = CompanyApplicationRegistrationPlanner.Build(request);

        Assert.NotEmpty(plan.ValidationErrors);
    }

    private static RegisterCompanyRequest CreateRequest(
        string companyName = "CI Home Service",
        string email = "direction@entreprise.ci",
        IReadOnlyList<string>? services = null)
    {
        return new RegisterCompanyRequest(
            companyName,
            null,
            "Abidjan",
            "Cocody",
            "John PriPri",
            email,
            "+2250700000000",
            "Password123",
            "Password123",
            services ?? ["Menage"],
            12);
    }
}
