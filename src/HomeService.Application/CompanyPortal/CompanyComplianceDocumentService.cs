using HomeService.Application.Abstractions;
using HomeService.Application.Companies;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.CompanyPortal;

public sealed class CompanyComplianceDocumentService(IAppDbContext db)
{
    public async Task<CompanyComplianceDocumentTargetResult> GetUploadTargetAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var companyExists = await db.Companies.AnyAsync(company => company.Id == companyId && company.Status != CompanyStatus.Suspended, cancellationToken);
        if (!companyExists)
        {
            return CompanyComplianceDocumentTargetResult.CompanyNotFound();
        }

        var applicationId = await db.CompanyApplications
            .Where(application => application.CompanyId == companyId)
            .OrderByDescending(application => application.CreatedAt)
            .Select(application => (Guid?)application.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return applicationId.HasValue
            ? CompanyComplianceDocumentTargetResult.Ok(applicationId.Value)
            : CompanyComplianceDocumentTargetResult.ApplicationNotFound();
    }

    public async Task<CompanyComplianceDocumentUploadResult> AttachDocumentsAsync(
        Guid companyId,
        IReadOnlyList<CompanyApplicationUploadedDocument> documents,
        CancellationToken cancellationToken)
    {
        if (documents.Count == 0)
        {
            return CompanyComplianceDocumentUploadResult.NoValidDocument();
        }

        var target = await GetUploadTargetAsync(companyId, cancellationToken);
        if (target.Status == CompanyComplianceDocumentStatus.CompanyNotFound)
        {
            return CompanyComplianceDocumentUploadResult.CompanyNotFound();
        }

        if (target.Status == CompanyComplianceDocumentStatus.ApplicationNotFound || target.ApplicationId is null)
        {
            return CompanyComplianceDocumentUploadResult.ApplicationNotFound();
        }

        foreach (var document in documents)
        {
            db.CompanyApplicationDocuments.Add(new CompanyApplicationDocument(
                target.ApplicationId.Value,
                document.DocumentType,
                document.OriginalFileName,
                document.StoragePath,
                document.ContentType));
        }

        db.CompanyApplicationStatusHistories.Add(new CompanyApplicationStatusHistory(
            target.ApplicationId.Value,
            null,
            CompanyApplicationStatus.Submitted,
            "Documents de conformite ajoutes depuis le portail entreprise.",
            "company-portal"));

        return CompanyComplianceDocumentUploadResult.Ok(
            target.ApplicationId.Value,
            documents.Count,
            documents.Select(document => document.DocumentType.ToString()).ToList());
    }
}
