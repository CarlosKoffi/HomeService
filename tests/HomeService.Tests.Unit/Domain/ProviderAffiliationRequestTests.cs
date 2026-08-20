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
        request.Approve("OK", true, true, true, true);

        Assert.Throws<InvalidOperationException>(() => request.Cancel("Trop tard."));
    }

    [Fact]
    public void Approve_StoresCompanyValidationAttestation()
    {
        var request = new ProviderAffiliationRequest(Guid.NewGuid(), Guid.NewGuid(), null);

        request.Approve("Test pratique concluant.", true, true, true, true);

        Assert.Equal(ProviderAffiliationRequestStatus.Approved, request.Status);
        Assert.True(request.CandidateMetAndTestedByCompany);
        Assert.True(request.CompetencyValidatedByCompany);
        Assert.True(request.SeriousnessValidatedByCompany);
        Assert.True(request.PunctualityValidatedByCompany);
        Assert.NotNull(request.CompanyValidationAttestedAt);
    }

    [Fact]
    public void Approve_Throws_WhenACompanyCheckIsMissing()
    {
        var request = new ProviderAffiliationRequest(Guid.NewGuid(), Guid.NewGuid(), null);

        Assert.Throws<InvalidOperationException>(() =>
            request.Approve("Test incomplet.", true, true, true, false));
    }
}
