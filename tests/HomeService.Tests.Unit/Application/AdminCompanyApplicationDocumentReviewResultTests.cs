using HomeService.Application.Admin;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Tests.Unit.Application;

public sealed class AdminCompanyApplicationDocumentReviewResultTests
{
    [Fact]
    public void Ok_CarriesDocumentAndPreviousStatus()
    {
        var document = CreateDocument();

        var result = AdminCompanyApplicationDocumentReviewResult.Ok(document, DocumentReviewStatus.Pending);

        Assert.Equal(AdminCompanyApplicationDocumentReviewStatus.Ok, result.Status);
        Assert.Same(document, result.Document);
        Assert.Equal(DocumentReviewStatus.Pending, result.PreviousStatus);
    }

    [Fact]
    public void ValidationFailed_CarriesMessage()
    {
        var result = AdminCompanyApplicationDocumentReviewResult.ValidationFailed("Commentaire obligatoire.");

        Assert.Equal(AdminCompanyApplicationDocumentReviewStatus.ValidationFailed, result.Status);
        Assert.Equal("Commentaire obligatoire.", result.Message);
        Assert.Null(result.Document);
    }

    [Fact]
    public void InvalidTransition_CarriesMessage()
    {
        var result = AdminCompanyApplicationDocumentReviewResult.InvalidTransition("Transition invalide.");

        Assert.Equal(AdminCompanyApplicationDocumentReviewStatus.InvalidTransition, result.Status);
        Assert.Equal("Transition invalide.", result.Message);
    }

    private static CompanyApplicationDocument CreateDocument()
    {
        return new CompanyApplicationDocument(
            Guid.NewGuid(),
            CompanyDocumentType.BusinessRegistration,
            "registre.pdf",
            "documents/registre.pdf",
            "application/pdf");
    }
}
