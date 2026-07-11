namespace HomeService.Application.CompanyPortal;

public sealed record CompanyComplianceDocumentUploadResult(
    CompanyComplianceDocumentStatus Status,
    Guid? ApplicationId,
    int DocumentCount,
    IReadOnlyList<string> DocumentTypes,
    string? Message)
{
    public static CompanyComplianceDocumentUploadResult Ok(Guid applicationId, int documentCount, IReadOnlyList<string> documentTypes)
        => new(CompanyComplianceDocumentStatus.Ok, applicationId, documentCount, documentTypes, "Documents recus. Notre equipe va les verifier.");

    public static CompanyComplianceDocumentUploadResult CompanyNotFound()
        => new(CompanyComplianceDocumentStatus.CompanyNotFound, null, 0, [], "Entreprise introuvable ou inactive.");

    public static CompanyComplianceDocumentUploadResult ApplicationNotFound()
        => new(CompanyComplianceDocumentStatus.ApplicationNotFound, null, 0, [], "Dossier entreprise introuvable.");

    public static CompanyComplianceDocumentUploadResult NoValidDocument()
        => new(CompanyComplianceDocumentStatus.NoValidDocument, null, 0, [], "Aucun document valide n'a ete transmis.");
}
