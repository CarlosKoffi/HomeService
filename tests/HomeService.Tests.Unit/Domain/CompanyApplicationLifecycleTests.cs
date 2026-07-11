using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Tests.Unit.Domain;

public sealed class CompanyApplicationLifecycleTests
{
    [Fact]
    public void Constructor_WhenIdProvided_UsesProvidedId()
    {
        var id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        var application = CreateApplication(id);

        Assert.Equal(id, application.Id);
    }

    [Fact]
    public void Approve_WhenApplicationIsSubmitted_MarksApplicationApproved()
    {
        var application = CreateApplication();

        application.Approve("admin");

        Assert.Equal(CompanyApplicationStatus.Approved, application.Status);
        Assert.NotNull(application.ReviewedAt);
    }

    [Fact]
    public void CreateActivationToken_WhenApplicationIsNotApproved_Throws()
    {
        var application = CreateApplication();

        Assert.Throws<InvalidOperationException>(() =>
            application.CreateActivationToken("hash", DateTimeOffset.UtcNow.AddHours(24), "https://company/activate"));
    }

    [Fact]
    public void CreateActivationToken_WhenReplacingExistingToken_RevokesPreviousToken()
    {
        var application = CreateApplication();
        application.Approve("admin");
        var first = application.CreateActivationToken("hash-1", DateTimeOffset.UtcNow.AddHours(24), "https://company/activate/1");

        var second = application.CreateActivationToken("hash-2", DateTimeOffset.UtcNow.AddHours(24), "https://company/activate/2");

        Assert.False(first.IsActive);
        Assert.True(second.IsActive);
        Assert.Equal(CompanyApplicationStatus.ActivationSent, application.Status);
    }

    [Fact]
    public void Reopen_WhenApplicationIsRejected_MarksUnderReview()
    {
        var application = CreateApplication();
        application.Reject("Document illisible", "admin");

        application.Reopen("Nouvelle analyse", "admin");

        Assert.Equal(CompanyApplicationStatus.UnderReview, application.Status);
        Assert.Equal("Nouvelle analyse", application.ReviewNote);
    }

    [Fact]
    public void Reopen_WhenApplicationIsNotRejected_Throws()
    {
        var application = CreateApplication();

        Assert.Throws<InvalidOperationException>(() => application.Reopen("Test", "admin"));
    }

    private static CompanyApplication CreateApplication(Guid? id = null)
    {
        return new CompanyApplication(
            "CI Home Service",
            null,
            "Abidjan",
            "Cocody",
            "John Pripri",
            "direction@entreprise.ci",
            "+2250701020304",
            "Menage, Jardinage",
            12,
            id);
    }
}
