using HomeService.Application.Admin;
using HomeService.Application.Auditing;
using HomeService.Application.Companies;
using HomeService.Application.CompanyPortal;
using HomeService.Application.Notifications;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class AdminCompanyApplicationDocumentReviewServiceTests
{
    [Fact]
    public async Task RejectAsync_WhenApplicationIsLinkedToCompany_UsesStablePortalEventKey()
    {
        await using var db = CreateDbContext();
        var company = new Company("wele Services", "+2250700000000", "contact@wele.ci");
        company.Approve();
        var application = new CompanyApplication(
            "wele Services",
            null,
            "Abidjan",
            "Cocody",
            "Awa Kone",
            "contact@wele.ci",
            "+2250700000000",
            "Menage",
            3);
        application.LinkPendingCompany(company.Id);
        var document = new CompanyApplicationDocument(
            application.Id,
            CompanyDocumentType.OwnerIdentity,
            "id.png",
            "company-applications/app/id.png",
            "image/png");

        db.Companies.Add(company);
        db.CompanyApplications.Add(application);
        db.CompanyApplicationDocuments.Add(document);
        AddRemainingRequiredDocuments(db, application.Id, document.DocumentType);
        await db.SaveChangesAsync();
        var sut = new AdminCompanyApplicationDocumentReviewService(
            db,
            new CompanyPortalNotificationWriter(db),
            new NotificationDeliveryPreferenceService(db));

        var result = await sut.RejectAsync(document.Id, "Photo illisible", CancellationToken.None);

        Assert.Equal(AdminCompanyApplicationDocumentReviewStatus.Ok, result.Status);
        var notification = await db.CompanyPortalNotifications.SingleAsync();
        Assert.Equal("CompanyDocumentRejected", notification.Type);
        Assert.Equal("Une piece de votre dossier a ete refusee", notification.Title);
        Assert.Equal("Photo illisible", notification.Message);
    }

    [Fact]
    public async Task ApproveAsync_WhenAuditActorIsProvided_PersistsAndAudits()
    {
        await using var db = CreateDbContext();
        var document = AddApplicationDocument(db);
        await db.SaveChangesAsync();
        var sut = CreateService(db);
        var auditContext = new AuditRequestContext("127.0.0.1", "unit-tests", "document-approve");

        var result = await sut.ApproveAsync(document.Id, AuditActor.Admin(), auditContext, CancellationToken.None);

        Assert.Equal(AdminCompanyApplicationDocumentReviewStatus.Ok, result.Status);
        var savedDocument = await db.CompanyApplicationDocuments.SingleAsync(entity => entity.Id == document.Id);
        Assert.Equal(DocumentReviewStatus.Approved, savedDocument.ReviewStatus);
        var audit = await db.AuditLogEntries.SingleAsync();
        Assert.Equal("AdminCompanyApplicationDocumentApproved", audit.Action);
        Assert.Equal(nameof(CompanyApplicationDocument), audit.EntityType);
        Assert.Equal(document.Id, audit.EntityId);
        Assert.Equal("document-approve", audit.CorrelationId);
    }

    [Fact]
    public async Task RequestReplacementAsync_WhenAuditActorIsProvided_PersistsAndAudits()
    {
        await using var db = CreateDbContext();
        var document = AddApplicationDocument(db);
        await db.SaveChangesAsync();
        var sut = CreateService(db);
        var auditContext = new AuditRequestContext("127.0.0.1", "unit-tests", "document-replacement");

        var result = await sut.RequestReplacementAsync(
            document.Id,
            "Image floue",
            AuditActor.Admin(),
            auditContext,
            CancellationToken.None);

        Assert.Equal(AdminCompanyApplicationDocumentReviewStatus.Ok, result.Status);
        var savedDocument = await db.CompanyApplicationDocuments.SingleAsync(entity => entity.Id == document.Id);
        Assert.Equal(DocumentReviewStatus.NeedsReplacement, savedDocument.ReviewStatus);
        Assert.Equal("Image floue", savedDocument.ReviewNote);
        var audit = await db.AuditLogEntries.SingleAsync();
        Assert.Equal("AdminCompanyApplicationDocumentReplacementRequested", audit.Action);
        Assert.Equal(document.Id, audit.EntityId);
        Assert.Equal("document-replacement", audit.CorrelationId);
        Assert.Contains("Image floue", audit.AfterJson);
    }

    [Fact]
    public async Task ReopenAsync_WhenAuditActorIsProvided_PersistsAndAudits()
    {
        await using var db = CreateDbContext();
        var document = AddApplicationDocument(db);
        document.Reject("Illisible");
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var result = await sut.ReopenAsync(
            document.Id,
            "Nouvelle analyse",
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "unit-tests", "document-reopen"),
            CancellationToken.None);

        Assert.Equal(AdminCompanyApplicationDocumentReviewStatus.Ok, result.Status);
        var savedDocument = await db.CompanyApplicationDocuments.SingleAsync(entity => entity.Id == document.Id);
        Assert.Equal(DocumentReviewStatus.Pending, savedDocument.ReviewStatus);
        Assert.Equal("Nouvelle analyse", savedDocument.ReviewNote);
        var audit = await db.AuditLogEntries.SingleAsync();
        Assert.Equal("AdminCompanyApplicationDocumentReopened", audit.Action);
        Assert.Equal(document.Id, audit.EntityId);
    }

    private static CompanyApplicationDocument AddApplicationDocument(HomeServiceDbContext db)
    {
        var application = new CompanyApplication(
            "wele Services",
            null,
            "Abidjan",
            "Cocody",
            "Awa Kone",
            "contact@wele.ci",
            "+2250700000000",
            "Menage",
            3);
        var document = new CompanyApplicationDocument(
            application.Id,
            CompanyDocumentType.BusinessRegistration,
            "rccm.png",
            "company-applications/app/rccm.png",
            "image/png");

        db.CompanyApplications.Add(application);
        db.CompanyApplicationDocuments.Add(document);
        AddRemainingRequiredDocuments(db, application.Id, document.DocumentType);

        return document;
    }

    private static void AddRemainingRequiredDocuments(
        HomeServiceDbContext db,
        Guid applicationId,
        CompanyDocumentType documentTypeAlreadyAdded)
    {
        foreach (var documentType in RequiredCompanyDocumentsPolicy.RequiredDocumentTypes
                     .Where(documentType => documentType != documentTypeAlreadyAdded))
        {
            db.CompanyApplicationDocuments.Add(new CompanyApplicationDocument(
                applicationId,
                documentType,
                $"{documentType}.png",
                $"company-applications/app/{documentType}.png",
                "image/png"));
        }
    }

    private static AdminCompanyApplicationDocumentReviewService CreateService(HomeServiceDbContext db)
    {
        return new AdminCompanyApplicationDocumentReviewService(
            db,
            new CompanyPortalNotificationWriter(db),
            new NotificationDeliveryPreferenceService(db));
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }
}
