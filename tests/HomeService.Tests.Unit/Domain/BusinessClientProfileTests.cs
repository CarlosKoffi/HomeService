using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Tests.Unit.Domain;

public sealed class BusinessClientProfileTests
{
    [Fact]
    public void Submit_WhenRequiredInformationIsMissing_Throws()
    {
        var profile = new BusinessClientProfile(Guid.NewGuid());

        var exception = Assert.Throws<InvalidOperationException>(profile.Submit);

        Assert.Contains("informations obligatoires", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(BusinessClientStatus.Draft, profile.Status);
    }

    [Fact]
    public void Submit_WhenRequiredInformationIsComplete_MarksProfileSubmitted()
    {
        var profile = CreateCompleteProfile();

        profile.Submit();

        Assert.Equal(BusinessClientStatus.Submitted, profile.Status);
        Assert.NotNull(profile.SubmittedAt);
        Assert.False(profile.CanEdit);
    }

    [Fact]
    public void RequestMoreInformation_ReopensProfileForCorrection()
    {
        var profile = CreateCompleteProfile();
        profile.Submit();
        profile.MarkUnderReview();

        profile.RequestMoreInformation("Remplacez la piece du representant.");

        Assert.Equal(BusinessClientStatus.MoreInformationRequested, profile.Status);
        Assert.True(profile.CanEdit);
        Assert.Equal("Remplacez la piece du representant.", profile.ReviewNote);
    }

    [Fact]
    public void Update_WhenProfileIsUnderReview_Throws()
    {
        var profile = CreateCompleteProfile();
        profile.Submit();
        profile.MarkUnderReview();

        Assert.Throws<InvalidOperationException>(() => UpdateProfile(profile));
    }

    [Fact]
    public void Document_RequestReplacement_RecordsReviewDecision()
    {
        var document = new BusinessClientDocument(
            Guid.NewGuid(),
            BusinessClientDocumentType.RepresentativeIdentity,
            "identite.pdf",
            "business-clients/document.pdf",
            "application/pdf",
            1_024);

        document.RequestReplacement("Document illisible.");

        Assert.Equal(DocumentReviewStatus.NeedsReplacement, document.ReviewStatus);
        Assert.Equal("Document illisible.", document.ReviewNote);
    }

    private static BusinessClientProfile CreateCompleteProfile()
    {
        var profile = new BusinessClientProfile(Guid.NewGuid());
        UpdateProfile(profile);
        return profile;
    }

    private static void UpdateProfile(BusinessClientProfile profile)
    {
        profile.Update(
            "Abidjan Residence Services",
            "ARS",
            "SARL",
            "CI-ABJ-2026-B-12345",
            "1234567A",
            "Cocody Riviera 3",
            "Abidjan",
            "CI",
            "Awa Kouame",
            "Gerante",
            "contact@ars.ci",
            "+2250700000000");
    }
}
