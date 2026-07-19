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
}
