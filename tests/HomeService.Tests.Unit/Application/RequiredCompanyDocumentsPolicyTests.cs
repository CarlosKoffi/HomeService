using HomeService.Application.Companies;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Tests.Unit.Application;

public sealed class RequiredCompanyDocumentsPolicyTests
{
    [Fact]
    public void HasAllRequiredApprovedDocuments_WhenAllRequiredDocumentsAreApproved_ReturnsTrue()
    {
        var documents = new[]
        {
            CreateDocument(CompanyDocumentType.FiscalExistenceDeclaration, DocumentReviewStatus.Approved),
            CreateDocument(CompanyDocumentType.BusinessRegistration, DocumentReviewStatus.Approved),
            CreateDocument(CompanyDocumentType.OwnerIdentity, DocumentReviewStatus.Approved),
            CreateDocument(CompanyDocumentType.SupportingDocument, DocumentReviewStatus.Pending)
        };

        Assert.True(RequiredCompanyDocumentsPolicy.HasAllRequiredApprovedDocuments(documents));
    }

    [Fact]
    public void HasAllRequiredApprovedDocuments_WhenRequiredDocumentIsRejected_ReturnsFalse()
    {
        var documents = new[]
        {
            CreateDocument(CompanyDocumentType.FiscalExistenceDeclaration, DocumentReviewStatus.Approved),
            CreateDocument(CompanyDocumentType.BusinessRegistration, DocumentReviewStatus.Rejected),
            CreateDocument(CompanyDocumentType.OwnerIdentity, DocumentReviewStatus.Approved)
        };

        Assert.False(RequiredCompanyDocumentsPolicy.HasAllRequiredApprovedDocuments(documents));
    }

    [Fact]
    public void GetMissingApprovedDocumentTypes_ReturnsOnlyMissingRequiredTypes()
    {
        var documents = new[]
        {
            CreateDocument(CompanyDocumentType.FiscalExistenceDeclaration, DocumentReviewStatus.Approved),
            CreateDocument(CompanyDocumentType.SupportingDocument, DocumentReviewStatus.Approved)
        };

        var missing = RequiredCompanyDocumentsPolicy.GetMissingApprovedDocumentTypes(documents);

        Assert.Contains(CompanyDocumentType.BusinessRegistration, missing);
        Assert.Contains(CompanyDocumentType.OwnerIdentity, missing);
        Assert.DoesNotContain(CompanyDocumentType.FiscalExistenceDeclaration, missing);
        Assert.DoesNotContain(CompanyDocumentType.SupportingDocument, missing);
    }

    private static CompanyApplicationDocument CreateDocument(CompanyDocumentType type, DocumentReviewStatus status)
    {
        var document = new CompanyApplicationDocument(Guid.NewGuid(), type, $"{type}.pdf", $"storage/{type}.pdf", "application/pdf");
        switch (status)
        {
            case DocumentReviewStatus.Approved:
                document.Approve();
                break;
            case DocumentReviewStatus.Rejected:
                document.Reject("Non conforme");
                break;
            case DocumentReviewStatus.NeedsReplacement:
                document.RequestReplacement("A remplacer");
                break;
        }

        return document;
    }
}
