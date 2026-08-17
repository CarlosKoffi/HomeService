using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Application.Companies;

public static class RequiredCompanyDocumentsPolicy
{
    public static IReadOnlyList<CompanyDocumentType> RequiredDocumentTypes { get; } =
    [
        CompanyDocumentType.FiscalExistenceDeclaration,
        CompanyDocumentType.BusinessRegistration,
        CompanyDocumentType.OwnerIdentity
    ];

    public static bool HasAllRequiredApprovedDocuments(IEnumerable<CompanyApplicationDocument> documents)
    {
        var approvedTypes = documents
            .Where(document => document.ReviewStatus == DocumentReviewStatus.Approved)
            .Select(document => document.DocumentType)
            .ToHashSet();

        return RequiredDocumentTypes.All(approvedTypes.Contains);
    }

    public static bool HasAllRequiredSubmittedDocuments(IEnumerable<CompanyApplicationDocument> documents)
        => GetMissingSubmittedDocumentTypes(documents).Count == 0;

    public static IReadOnlyList<CompanyDocumentType> GetMissingSubmittedDocumentTypes(IEnumerable<CompanyApplicationDocument> documents)
    {
        var submittedTypes = documents
            .Where(document => document.ReviewStatus is DocumentReviewStatus.Pending or DocumentReviewStatus.Approved)
            .Select(document => document.DocumentType)
            .ToHashSet();

        return RequiredDocumentTypes
            .Where(requiredType => !submittedTypes.Contains(requiredType))
            .ToList();
    }

    public static IReadOnlyList<CompanyDocumentType> GetMissingApprovedDocumentTypes(IEnumerable<CompanyApplicationDocument> documents)
    {
        var approvedTypes = documents
            .Where(document => document.ReviewStatus == DocumentReviewStatus.Approved)
            .Select(document => document.DocumentType)
            .ToHashSet();

        return RequiredDocumentTypes
            .Where(requiredType => !approvedTypes.Contains(requiredType))
            .ToList();
    }
}
