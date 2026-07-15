using HomeService.Application.Abstractions;
using HomeService.Application.Companies;
using HomeService.Application.Notifications;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminCompanyApplicationReviewService(IAppDbContext db)
{
    private const string AdminActor = "admin";

    public async Task<AdminCompanyApplicationReviewResult> ApproveAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var application = await db.CompanyApplications
            .Include(application => application.Documents)
            .FirstOrDefaultAsync(application => application.Id == applicationId, cancellationToken);
        if (application is null)
        {
            return AdminCompanyApplicationReviewResult.NotFound();
        }

        if (!RequiredCompanyDocumentsPolicy.HasAllRequiredApprovedDocuments(application.Documents))
        {
            return AdminCompanyApplicationReviewResult.MissingRequiredDocuments("Les pieces obligatoires doivent etre validees avant validation du dossier.");
        }

        var previousStatus = application.Status;
        application.Approve(AdminActor);
        AddStatusHistory(application.Id, previousStatus, application.Status, null);

        if (application.CompanyId is null)
        {
            var company = new Company(application.CompanyName, application.PhoneNumber, application.Email);
            SyncCompanyFromApplication(company, application);
            company.Approve();
            db.Companies.Add(company);
            application.LinkApprovedCompany(company.Id, AdminActor);
        }
        else
        {
            var company = await db.Companies.FirstOrDefaultAsync(company => company.Id == application.CompanyId, cancellationToken);
            if (company is not null)
            {
                SyncCompanyFromApplication(company, application);
                company.Approve();
            }
        }

        return AdminCompanyApplicationReviewResult.Ok(application, previousStatus);
    }

    private static void SyncCompanyFromApplication(Company company, CompanyApplication application)
    {
        company.UpdateCompanyInformation(
            application.CompanyName,
            application.LegalForm,
            application.RegistrationNumber,
            application.TaxIdentificationNumber,
            application.City,
            application.Address);
        company.UpdateOperations(application.InterventionZones, application.PlannedServices);
        company.UpdatePayment(application.WavePaymentNumber, application.OrangeMoneyPaymentNumber);
    }

    public async Task<AdminCompanyApplicationReviewResult> RejectAsync(Guid applicationId, string? note, CancellationToken cancellationToken)
    {
        var reviewNote = ReviewNoteValidator.GetRequired(note, "Une note est obligatoire pour refuser une demande entreprise.");
        if (reviewNote.ErrorMessage is not null)
        {
            return AdminCompanyApplicationReviewResult.ValidationFailed(reviewNote.ErrorMessage);
        }

        var application = await db.CompanyApplications.FirstOrDefaultAsync(application => application.Id == applicationId, cancellationToken);
        if (application is null)
        {
            return AdminCompanyApplicationReviewResult.NotFound();
        }

        var previousStatus = application.Status;
        application.Reject(reviewNote.Value!, AdminActor);
        AddStatusHistory(application.Id, previousStatus, application.Status, reviewNote.Value);
        db.NotificationOutboxMessages.AddRange(CompanyApplicationNotificationFactory.Rejected(application, reviewNote.Value!));
        return AdminCompanyApplicationReviewResult.Ok(application, previousStatus);
    }

    public async Task<AdminCompanyApplicationReviewResult> ReopenAsync(Guid applicationId, string? note, CancellationToken cancellationToken)
    {
        var reviewNote = ReviewNoteValidator.GetRequired(note, "Une note est obligatoire pour reouvrir une demande refusee.");
        if (reviewNote.ErrorMessage is not null)
        {
            return AdminCompanyApplicationReviewResult.ValidationFailed(reviewNote.ErrorMessage);
        }

        var application = await db.CompanyApplications.FirstOrDefaultAsync(application => application.Id == applicationId, cancellationToken);
        if (application is null)
        {
            return AdminCompanyApplicationReviewResult.NotFound();
        }

        var previousStatus = application.Status;
        try
        {
            application.Reopen(reviewNote.Value!, AdminActor);
        }
        catch (InvalidOperationException exception)
        {
            return AdminCompanyApplicationReviewResult.InvalidTransition(exception.Message);
        }

        AddStatusHistory(application.Id, previousStatus, application.Status, reviewNote.Value);
        db.NotificationOutboxMessages.AddRange(CompanyApplicationNotificationFactory.Reopened(application, reviewNote.Value!));
        return AdminCompanyApplicationReviewResult.Ok(application, previousStatus);
    }

    public async Task<AdminCompanyApplicationReviewResult> RequestMoreInformationAsync(Guid applicationId, string? note, CancellationToken cancellationToken)
    {
        var reviewNote = ReviewNoteValidator.GetRequired(note, "Une note est obligatoire pour demander un complement.");
        if (reviewNote.ErrorMessage is not null)
        {
            return AdminCompanyApplicationReviewResult.ValidationFailed(reviewNote.ErrorMessage);
        }

        var application = await db.CompanyApplications.FirstOrDefaultAsync(application => application.Id == applicationId, cancellationToken);
        if (application is null)
        {
            return AdminCompanyApplicationReviewResult.NotFound();
        }

        var previousStatus = application.Status;
        application.RequestMoreInformation(reviewNote.Value!, AdminActor);
        AddStatusHistory(application.Id, previousStatus, application.Status, reviewNote.Value);
        db.NotificationOutboxMessages.AddRange(CompanyApplicationNotificationFactory.MoreInformationRequested(application, reviewNote.Value!));
        return AdminCompanyApplicationReviewResult.Ok(application, previousStatus);
    }

    private void AddStatusHistory(
        Guid companyApplicationId,
        CompanyApplicationStatus? previousStatus,
        CompanyApplicationStatus newStatus,
        string? note)
    {
        if (previousStatus.HasValue && previousStatus.Value == newStatus)
        {
            return;
        }

        db.CompanyApplicationStatusHistories.Add(new CompanyApplicationStatusHistory(
            companyApplicationId,
            previousStatus,
            newStatus,
            note,
            AdminActor));
    }
}
