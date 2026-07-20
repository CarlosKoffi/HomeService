using HomeService.Domain.Entities;

namespace HomeService.Application.CompanyPortal;

public sealed record CompanyEmployeeDocumentOperationResult(
    CompanyEmployeeOperationStatus Status,
    ProviderProfile? Provider,
    ProviderDocument? Document,
    Guid? DocumentId,
    IReadOnlyList<string> ReplacedStoragePaths,
    object? Before,
    object? After,
    string? Message)
{
    public static CompanyEmployeeDocumentOperationResult Ok(
        ProviderProfile provider,
        ProviderDocument document,
        IReadOnlyList<string> replacedStoragePaths,
        object? before,
        object? after)
        => new(CompanyEmployeeOperationStatus.Ok, provider, document, document.Id, replacedStoragePaths, before, after, null);

    public static CompanyEmployeeDocumentOperationResult Ok(
        ProviderProfile provider,
        Guid documentId,
        IReadOnlyList<string> replacedStoragePaths,
        object? before,
        object? after)
        => new(CompanyEmployeeOperationStatus.Ok, provider, null, documentId, replacedStoragePaths, before, after, null);

    public static CompanyEmployeeDocumentOperationResult NotFound(string message = "Employe introuvable.")
        => new(CompanyEmployeeOperationStatus.NotFound, null, null, null, [], null, null, message);
}
