using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Tests.Unit.Domain;

public sealed class CompanyApplicationDocumentTests
{
    [Fact]
    public void Approve_MarksDocumentApproved()
    {
        var document = CreateDocument();

        document.Approve();

        Assert.Equal(DocumentReviewStatus.Approved, document.ReviewStatus);
    }

    [Fact]
    public void Reject_StoresReviewNote()
    {
        var document = CreateDocument();

        document.Reject("Fichier illisible");

        Assert.Equal(DocumentReviewStatus.Rejected, document.ReviewStatus);
        Assert.Equal("Fichier illisible", document.ReviewNote);
    }

    [Fact]
    public void RequestReplacement_MarksDocumentAsNeedsReplacement()
    {
        var document = CreateDocument();

        document.RequestReplacement("Merci de renvoyer le DFE complet");

        Assert.Equal(DocumentReviewStatus.NeedsReplacement, document.ReviewStatus);
        Assert.Equal("Merci de renvoyer le DFE complet", document.ReviewNote);
    }

    [Fact]
    public void Reopen_WhenRejected_MarksDocumentPending()
    {
        var document = CreateDocument();
        document.Reject("Fichier illisible");

        document.Reopen("Nouvelle piece recue");

        Assert.Equal(DocumentReviewStatus.Pending, document.ReviewStatus);
        Assert.Equal("Nouvelle piece recue", document.ReviewNote);
    }

    [Fact]
    public void Reopen_WhenNotRejected_Throws()
    {
        var document = CreateDocument();

        Assert.Throws<InvalidOperationException>(() => document.Reopen("Test"));
    }

    private static CompanyApplicationDocument CreateDocument()
    {
        return new CompanyApplicationDocument(
            Guid.NewGuid(),
            CompanyDocumentType.FiscalExistenceDeclaration,
            "dfe.pdf",
            "companies/app/dfe.pdf",
            "application/pdf");
    }
}
