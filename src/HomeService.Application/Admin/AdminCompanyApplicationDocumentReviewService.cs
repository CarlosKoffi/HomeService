using HomeService.Application.Abstractions;
using HomeService.Application.Auditing;
using HomeService.Application.CompanyPortal;
using HomeService.Application.Companies;
using HomeService.Application.Notifications;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminCompanyApplicationDocumentReviewService(
    IAppDbContext db,
    CompanyPortalNotificationWriter portalNotifications,
    NotificationDeliveryPreferenceService deliveryPreferences)
{
    public async Task<AdminCompanyApplicationDocumentReviewResult> ApproveAsync(Guid documentId, CancellationToken cancellationToken)
        => await ApproveAsync(documentId, null, null, cancellationToken);

    public async Task<AdminCompanyApplicationDocumentReviewResult> ApproveAsync(
        Guid documentId,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        var document = await db.CompanyApplicationDocuments
            .Include(document => document.CompanyApplication)
            .FirstOrDefaultAsync(document => document.Id == documentId, cancellationToken);
        if (document is null)
        {
            return AdminCompanyApplicationDocumentReviewResult.NotFound();
        }

        var incompleteResult = await EnsureCompleteDossierAsync(document.CompanyApplicationId, cancellationToken);
        if (incompleteResult is not null)
        {
            return incompleteResult;
        }

        var previousStatus = document.ReviewStatus;
        var before = ToAuditSnapshot(document, previousStatus);
        document.Approve();
        if (document.CompanyApplication is not null)
        {
            portalNotifications.AddForDocument(
                document.CompanyApplication,
                document,
                "CompanyDocumentApproved",
                "Piece validee",
                $"{GetDocumentLabel(document.DocumentType)} a ete validee.",
                "success");
        }

        AddAuditLog(
            actor,
            auditContext,
            "AdminCompanyApplicationDocumentApproved",
            "Piece entreprise validee.",
            document,
            before);
        await db.SaveChangesAsync(cancellationToken);

        return AdminCompanyApplicationDocumentReviewResult.Ok(document, previousStatus);
    }

    public Task<AdminCompanyApplicationDocumentReviewResult> RejectAsync(Guid documentId, string? comment, CancellationToken cancellationToken)
        => RejectAsync(documentId, comment, null, null, cancellationToken);

    public Task<AdminCompanyApplicationDocumentReviewResult> RejectAsync(
        Guid documentId,
        string? comment,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        return ReviewWithRequiredCommentAsync(
            documentId,
            comment,
            "Un commentaire est obligatoire pour refuser une piece.",
            document => document.Reject(comment!.Trim()),
            "CompanyDocumentRejected",
            "Une piece de votre dossier a ete refusee",
            $"Une piece de votre dossier entreprise a ete refusee. Commentaire: {comment?.Trim()}",
            actor,
            auditContext,
            "AdminCompanyApplicationDocumentRejected",
            "Piece entreprise refusee.",
            true,
            cancellationToken);
    }

    public Task<AdminCompanyApplicationDocumentReviewResult> RequestReplacementAsync(Guid documentId, string? comment, CancellationToken cancellationToken)
        => RequestReplacementAsync(documentId, comment, null, null, cancellationToken);

    public Task<AdminCompanyApplicationDocumentReviewResult> RequestReplacementAsync(
        Guid documentId,
        string? comment,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        return ReviewWithRequiredCommentAsync(
            documentId,
            comment,
            "Un commentaire est obligatoire pour demander le remplacement d'une piece.",
            document => document.RequestReplacement(comment!.Trim()),
            "CompanyDocumentNeedsReplacement",
            "Complement de piece requis",
            $"Une piece de votre dossier entreprise doit etre remplacee ou completee. Commentaire: {comment?.Trim()}",
            actor,
            auditContext,
            "AdminCompanyApplicationDocumentReplacementRequested",
            "Remplacement de piece entreprise demande.",
            true,
            cancellationToken);
    }

    public Task<AdminCompanyApplicationDocumentReviewResult> ReopenAsync(Guid documentId, string? comment, CancellationToken cancellationToken)
        => ReopenAsync(documentId, comment, null, null, cancellationToken);

    public Task<AdminCompanyApplicationDocumentReviewResult> ReopenAsync(
        Guid documentId,
        string? comment,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        return ReviewWithRequiredCommentAsync(
            documentId,
            comment,
            "Un commentaire est obligatoire pour reouvrir une piece refusee.",
            document => document.Reopen(comment!.Trim()),
            "CompanyDocumentReopened",
            "Une piece refusee est reouverte",
            $"Une piece de votre dossier a ete reouverte pour verification. Commentaire: {comment?.Trim()}",
            actor,
            auditContext,
            "AdminCompanyApplicationDocumentReopened",
            "Piece entreprise reouverte.",
            false,
            cancellationToken);
    }

    private async Task<AdminCompanyApplicationDocumentReviewResult> ReviewWithRequiredCommentAsync(
        Guid documentId,
        string? comment,
        string requiredMessage,
        Action<CompanyApplicationDocument> applyReview,
        string notificationEventKey,
        string notificationSubject,
        string notificationBody,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        string auditAction,
        string auditSummary,
        bool requireCompleteDossier,
        CancellationToken cancellationToken)
    {
        var reviewComment = ReviewNoteValidator.GetRequired(comment, requiredMessage);
        if (reviewComment.ErrorMessage is not null)
        {
            return AdminCompanyApplicationDocumentReviewResult.ValidationFailed(reviewComment.ErrorMessage);
        }

        var document = await db.CompanyApplicationDocuments
            .Include(document => document.CompanyApplication)
            .FirstOrDefaultAsync(document => document.Id == documentId, cancellationToken);
        if (document is null)
        {
            return AdminCompanyApplicationDocumentReviewResult.NotFound();
        }


        if (requireCompleteDossier)
        {
            var incompleteResult = await EnsureCompleteDossierAsync(document.CompanyApplicationId, cancellationToken);
            if (incompleteResult is not null)
            {
                return incompleteResult;
            }
        }

        var previousStatus = document.ReviewStatus;
        var before = ToAuditSnapshot(document, previousStatus);
        try
        {
            applyReview(document);
        }
        catch (InvalidOperationException exception)
        {
            return AdminCompanyApplicationDocumentReviewResult.InvalidTransition(exception.Message);
        }

        await QueueDocumentNotificationAsync(
            document.CompanyApplicationId,
            notificationEventKey,
            notificationSubject,
            notificationBody,
            cancellationToken);
        if (document.CompanyApplication is not null)
        {
            portalNotifications.AddForDocument(
                document.CompanyApplication,
                document,
                notificationEventKey,
                notificationSubject,
                reviewComment.Value!,
                GetTone(document.ReviewStatus));
        }

        AddAuditLog(actor, auditContext, auditAction, auditSummary, document, before);
        await db.SaveChangesAsync(cancellationToken);

        return AdminCompanyApplicationDocumentReviewResult.Ok(document, previousStatus);
    }

    private async Task<AdminCompanyApplicationDocumentReviewResult?> EnsureCompleteDossierAsync(
        Guid companyApplicationId,
        CancellationToken cancellationToken)
    {
        var documents = await db.CompanyApplicationDocuments
            .AsNoTracking()
            .Where(document => document.CompanyApplicationId == companyApplicationId)
            .ToListAsync(cancellationToken);
        var missing = RequiredCompanyDocumentsPolicy.GetMissingSubmittedDocumentTypes(documents);
        return missing.Count == 0
            ? null
            : AdminCompanyApplicationDocumentReviewResult.ValidationFailed(
                $"Le controle est bloque tant que le depot obligatoire n'est pas complet. Pieces manquantes : {string.Join(", ", missing)}.");
    }

    private async Task QueueDocumentNotificationAsync(
        Guid companyApplicationId,
        string notificationEventKey,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        var application = await db.CompanyApplications.FirstOrDefaultAsync(application => application.Id == companyApplicationId, cancellationToken);
        if (application is null)
        {
            return;
        }

        var preference = await deliveryPreferences.GetAsync(
            notificationEventKey,
            "Company",
            defaultEmailEnabled: true,
            defaultWhatsAppEnabled: true,
            cancellationToken);

        db.NotificationOutboxMessages.AddRange(CompanyApplicationNotificationFactory.CreateApplicantNotifications(
            application,
            subject,
            body,
            preference.EmailEnabled,
            preference.WhatsAppEnabled));
    }

    private static string GetTone(HomeService.Domain.Enums.DocumentReviewStatus status) => status switch
    {
        HomeService.Domain.Enums.DocumentReviewStatus.Approved => "success",
        HomeService.Domain.Enums.DocumentReviewStatus.Pending => "warning",
        _ => "danger"
    };

    private static string GetDocumentLabel(HomeService.Domain.Enums.CompanyDocumentType documentType) => documentType switch
    {
        HomeService.Domain.Enums.CompanyDocumentType.FiscalExistenceDeclaration => "DFE",
        HomeService.Domain.Enums.CompanyDocumentType.BusinessRegistration => "Registre de commerce",
        HomeService.Domain.Enums.CompanyDocumentType.OwnerIdentity => "Identite du responsable",
        HomeService.Domain.Enums.CompanyDocumentType.AddressProof => "Justificatif d'adresse",
        _ => "Piece du dossier"
    };

    private void AddAuditLog(
        AuditActor? actor,
        AuditRequestContext? auditContext,
        string action,
        string summary,
        CompanyApplicationDocument document,
        object before)
    {
        if (actor is null)
        {
            return;
        }

        db.AuditLogEntries.Add(AuditLogFactory.Create(
            actor,
            action,
            nameof(CompanyApplicationDocument),
            document.Id,
            summary,
            auditContext,
            before,
            after: new { document.ReviewStatus, document.ReviewNote }));
    }

    private static object ToAuditSnapshot(CompanyApplicationDocument document, DocumentReviewStatus status)
    {
        return new
        {
            document.Id,
            document.CompanyApplicationId,
            ReviewStatus = status,
            document.ReviewNote
        };
    }
}
