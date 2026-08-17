using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Application.Providers;

public static class RequiredProviderDocumentsPolicy
{
    public static IReadOnlyList<ProviderDocumentType> RequiredDocumentTypes { get; } =
    [
        ProviderDocumentType.Photo,
        ProviderDocumentType.IdentityDocument,
        ProviderDocumentType.Diploma
    ];

    public static bool HasAllRequiredDocuments(IEnumerable<ProviderDocument> documents)
        => GetMissingDocumentTypes(documents).Count == 0;

    public static IReadOnlyList<ProviderDocumentType> GetMissingDocumentTypes(IEnumerable<ProviderDocument> documents)
    {
        var presentTypes = documents.Select(document => document.DocumentType).ToHashSet();
        return RequiredDocumentTypes.Where(type => !presentTypes.Contains(type)).ToArray();
    }
}
