using HomeService.Application.Abstractions;
using HomeService.Application.Notifications;
using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminCompanyApplicationDocumentReviewService(IAppDbContext db)
{
    public async Task<AdminCompanyApplicationDocumentReviewResult> ApproveAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var document = await db.CompanyApplicationDocuments.FirstOrDefaultAsync(document => document.Id == documentId, cancellationToken);
        if (document is null)
        {
            return AdminCompanyApplicationDocumentReviewResult.NotFound();
        }

        var previousStatus = document.ReviewStatus;
        document.Approve();
        return AdminCompanyApplicationDocumentReviewResult.Ok(document, previousStatus);
    }

    public Task<AdminCompanyApplicationDocumentReviewResult> RejectAsync(Guid documentId, string? comment, CancellationToken cancellationToken)
    {
        return ReviewWithRequiredCommentAsync(
            documentId,
            comment,
            "Un commentaire est obligatoire pour refuser une piece.",
            document => document.Reject(comment!.Trim()),
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
            "Une piece refusee est reouverte",
            $"Une piece de votre dossier a ete reouverte pour verification. Commentaire: {comment?.Trim()}",
            cancellationToken);
    }

    private async Task<AdminCompanyApplicationDocumentReviewResult> ReviewWithRequiredCommentAsync(
        Guid documentId,
        string? comment,
        string requiredMessage,
        Action<CompanyApplicationDocument> applyReview,
        string notificationSubject,
        string notificationBody,
        CancellationToken cancellationToken)
    {
        var reviewComment = ReviewNoteValidator.GetRequired(comment, requiredMessage);
        if (reviewComment.ErrorMessage is not null)
        {
            return AdminCompanyApplicationDocumentReviewResult.ValidationFailed(reviewComment.ErrorMessage);
        }

        var document = await db.CompanyApplicationDocuments.FirstOrDefaultAsync(document => document.Id == documentId, cancellationToken);
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

        await QueueDocumentNotificationAsync(document.CompanyApplicationId, notificationSubject, notificationBody, cancellationToken);
        return AdminCompanyApplicationDocumentReviewResult.Ok(document, previousStatus);
    }

    private async Task QueueDocumentNotificationAsync(
        Guid companyApplicationId,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        var application = await db.CompanyApplications.FirstOrDefaultAsync(application => application.Id == companyApplicationId, cancellationToken);
        if (application is null)
        {
            return;
        }

        db.NotificationOutboxMessages.AddRange(CompanyApplicationNotificationFactory.CreateApplicantNotifications(
            application,
            subject,
            body,
            includeWhatsApp: true));
    }
}
