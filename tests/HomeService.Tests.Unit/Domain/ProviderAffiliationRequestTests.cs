using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Tests.Unit.Domain;

public sealed class ProviderAffiliationRequestTests
{
    [Fact]
    public void Cancel_ClosesPendingRequestWithReviewNote()
    {
        var request = new ProviderAffiliationRequest(Guid.NewGuid(), Guid.NewGuid(), "Disponible pour un entretien.");

        request.Cancel("Valide par une autre entreprise.");

        Assert.Equal(ProviderAffiliationRequestStatus.Cancelled, request.Status);
        Assert.Equal("Valide par une autre entreprise.", request.ReviewNote);
        Assert.NotNull(request.ReviewedAt);
    }

    [Fact]
    public void Cancel_Throws_WhenRequestIsAlreadyApproved()
    {
        var request = new ProviderAffiliationRequest(Guid.NewGuid(), Guid.NewGuid(), null);
        request.Approve("OK");

        Assert.Throws<InvalidOperationException>(() => request.Cancel("Trop tard."));
    }
}
