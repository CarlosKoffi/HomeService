using HomeService.Application.ProviderPortal;

namespace HomeService.Tests.Unit.Application;

public sealed class ProviderCompanyOpportunityMatchingPolicyTests
{
    [Fact]
    public void Matches_ReturnsTrue_WhenCompanyHasParentService()
    {
        var companyId = Guid.NewGuid();
        var input = new ProviderCompanyWorkMatchInput(
            companyId,
            null,
            null,
            new HashSet<Guid> { companyId },
            new HashSet<Guid>(),
            new HashSet<Guid>(),
            ["blanchisserie", "repassage"]);

        Assert.True(ProviderCompanyOpportunityMatchingPolicy.Matches(input));
    }

    [Fact]
    public void Matches_ReturnsTrue_WhenCompanyTextMentionsChildPrestation()
    {
        var companyId = Guid.NewGuid();
        var input = new ProviderCompanyWorkMatchInput(
            companyId,
            "Pressing, lavage et repassage a domicile",
            null,
            new HashSet<Guid>(),
            new HashSet<Guid>(),
            new HashSet<Guid>(),
            ["blanchisserie", "repassage"]);

        Assert.True(ProviderCompanyOpportunityMatchingPolicy.Matches(input));
    }

    [Fact]
    public void Matches_ReturnsTrue_WhenApplicationTextMentionsParentService()
    {
        var companyId = Guid.NewGuid();
        var input = new ProviderCompanyWorkMatchInput(
            companyId,
            null,
            "Blanchisserie et entretien du linge",
            new HashSet<Guid>(),
            new HashSet<Guid>(),
            new HashSet<Guid>(),
            ["blanchisserie", "repassage"]);

        Assert.True(ProviderCompanyOpportunityMatchingPolicy.Matches(input));
    }

    [Fact]
    public void Matches_ReturnsFalse_WhenCompanyDoesNotCoverRequestedWork()
    {
        var input = new ProviderCompanyWorkMatchInput(
            Guid.NewGuid(),
            "Depannage auto",
            "Jardinage",
            new HashSet<Guid>(),
            new HashSet<Guid>(),
            new HashSet<Guid>(),
            ["blanchisserie", "repassage"]);

        Assert.False(ProviderCompanyOpportunityMatchingPolicy.Matches(input));
    }
}
