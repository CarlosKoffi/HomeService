using HomeService.Application.ProviderPortal;
using HomeService.Contracts.ProviderPortal;

namespace HomeService.Tests.Unit.Application;

public sealed class ProviderSelfRegistrationValidatorTests
{
    [Fact]
    public void Validate_ReturnsError_WhenNoOpportunityCompanyIsSelected()
    {
        var request = CreateValidRequest(opportunityCompanyIds: []);

        var errors = ProviderSelfRegistrationValidator.Validate(
            request,
            resolvedServiceCount: 1,
            selectedOpportunityCount: 0);

        Assert.Contains(errors, error => error.Contains("entreprise", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ReturnsError_WhenNoServiceOrPrestationIsResolved()
    {
        var request = CreateValidRequest([Guid.NewGuid()]);

        var errors = ProviderSelfRegistrationValidator.Validate(
            request,
            resolvedServiceCount: 0,
            selectedOpportunityCount: 1);

        Assert.Contains(errors, error => error.Contains("service", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ReturnsNoError_WhenServiceAndOpportunityArePresent()
    {
        var request = CreateValidRequest([Guid.NewGuid()]);

        var errors = ProviderSelfRegistrationValidator.Validate(
            request,
            resolvedServiceCount: 1,
            selectedOpportunityCount: 1);

        Assert.Empty(errors);
    }

    private static ProviderSelfRegistrationRequest CreateValidRequest(IReadOnlyList<Guid> opportunityCompanyIds)
    {
        var serviceId = Guid.NewGuid();
        return new ProviderSelfRegistrationRequest(
            "Awa",
            "Kone",
            "+2250700000000",
            new DateOnly(1998, 1, 1),
            "Cocody Angre",
            "Female",
            3,
            null,
            null,
            5,
            "Testeur123",
            "Testeur123",
            [new ProviderCandidateServiceRequest(serviceId, "Confirmed", 3)],
            [],
            [new ProviderCandidateSelectionRequest("Service", serviceId, "Confirmed", 3)],
            opportunityCompanyIds);
    }
}
