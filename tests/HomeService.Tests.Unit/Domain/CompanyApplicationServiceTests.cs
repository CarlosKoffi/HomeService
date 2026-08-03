using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Tests.Unit.Domain;

public sealed class CompanyApplicationServiceTests
{
    [Fact]
    public void MarkCreatedAsNewService_LinksProposalToNewService()
    {
        var proposal = new CompanyApplicationService(Guid.NewGuid(), "Repassage premium");
        var serviceId = Guid.NewGuid();

        proposal.MarkCreatedAsNewService(serviceId);

        Assert.Equal(serviceId, proposal.MatchedServiceId);
        Assert.Null(proposal.MatchedServicePrestationId);
        Assert.Equal(CompanyApplicationServiceMatchStatus.CreatedAsNewService, proposal.MatchStatus);
    }

    [Fact]
    public void Constructor_NormalizesProposalLikeCatalogNames()
    {
        var proposal = new CompanyApplicationService(Guid.NewGuid(), " Blanchisserie - Repassage ");

        Assert.Equal("blanchisserie repassage", proposal.NormalizedName);
    }

    [Fact]
    public void Reject_ClearsMatchesAndKeepsReviewReason()
    {
        var proposal = new CompanyApplicationService(Guid.NewGuid(), "Location de materiel");
        proposal.MarkCreatedAsNewService(Guid.NewGuid());

        proposal.Reject("  Hors catalogue Wele  ");

        Assert.Null(proposal.MatchedServiceId);
        Assert.Null(proposal.MatchedServicePrestationId);
        Assert.Null(proposal.MatchScore);
        Assert.Equal(CompanyApplicationServiceMatchStatus.Rejected, proposal.MatchStatus);
        Assert.Equal("Hors catalogue Wele", proposal.ReviewNote);
    }
}
