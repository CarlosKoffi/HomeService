using HomeService.Application.Abstractions;
using HomeService.Application.CompanyPortal;
using HomeService.Application.Notifications;
using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminCompanyApplicationDocumentReviewService(
    IAppDbContext db,
    CompanyPortalNotificationWriter portalNotifications,
    NotificationDeliveryPreferenceService deliveryPreferences)
{
    public async Task<AdminCompanyApplicationDocumentReviewResult> ApproveAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var document = await db.CompanyApplicationDocuments
            .Include(document => document.CompanyApplication)
            .FirstOrDefaultAsync(document => document.Id == documentId, cancellationToken);
        if (document is null)
        {
            return AdminCompanyApplicationDocumentReviewResult.NotFound();
        }

        var previousStatus = document.ReviewStatus;
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

        return AdminCompanyApplicationDocumentReviewResult.Ok(document, previousStatus);
    }

    public Task<AdminCompanyApplicationDocumentReviewResult> RejectAsync(Guid documentId, string? comment, CancellationToken cancellationToken)
    {
        return ReviewWithRequiredCommentAsync(
            documentId,
            comment,
            "Un commentaire est obligatoire pour refuser une piece.",
            document => document.Reject(comment!.Trim()),
            "CompanyDocumentRejected",
            "Une piece de votre dossier a ete refusee",
            $"Une piece de votre dossier entreprise a ete refusee. Commentaire: {comment?.Trim()}",
            cancellationToken);
    }

    public Task<AdminCompanyApplicationDocumentReviewResult> RequestReplacementAsync(Guid documentId, string? comment, CancellationToken cancellationToken)
    {
        return ReviewWithRequiredCommentAsync(
            documentId,
            comment,
            "Un commentaire est obligatoire pour demander le remplacement d'une piece.",
            document => document.RequestReplacement(comment!.Trim()),
            "CompanyDocumentNeedsReplacement",
            "Complement de piece requis",
            $"Une piece de votre dossier entreprise doit etre remplacee ou completee. Commentaire: {comment?.Trim()}",
            cancellationToken);
    }

    public Task<AdminCompanyApplicationDocumentReviewResult> ReopenAsync(Guid documentId, string? comment, CancellationToken cancellationToken)
    {
        return ReviewWithRequiredCommentAsync(
            documentId,
            comment,
            "Un commentaire est obligatoire pour reouvrir une piece refusee.",
            document => document.Reopen(comment!.Trim()),
            "CompanyDocumentReopened",
            "Une piece refusee est reouverte",
            $"Une piece de votre dossier a ete reouverte pour verification. Commentaire: {comment?.Trim()}",
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

        var previousStatus = document.ReviewStatus;
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
                notificationSubject,
                notificationSubject,
                reviewComment.Value!,
                GetTone(document.ReviewStatus));
        }

        return AdminCompanyApplicationDocumentReviewResult.Ok(document, previousStatus);
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
}
