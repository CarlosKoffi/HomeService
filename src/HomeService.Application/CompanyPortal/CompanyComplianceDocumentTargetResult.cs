namespace HomeService.Application.CompanyPortal;

public sealed record CompanyComplianceDocumentTargetResult(
    CompanyComplianceDocumentStatus Status,
    Guid? ApplicationId,
    string? Message)
{
    public static CompanyComplianceDocumentTargetResult Ok(Guid applicationId)
        => new(CompanyComplianceDocumentStatus.Ok, applicationId, null);

    public static CompanyComplianceDocumentTargetResult CompanyNotFound()
        => new(CompanyComplianceDocumentStatus.CompanyNotFound, null, "Entreprise introuvable ou inactive.");

    public static CompanyComplianceDocumentTargetResult ApplicationNotFound()
        => new(CompanyComplianceDocumentStatus.ApplicationNotFound, null, "Dossier entreprise introuvable.");
}
