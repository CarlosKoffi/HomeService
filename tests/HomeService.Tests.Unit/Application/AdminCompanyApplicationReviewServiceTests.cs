using HomeService.Application.Admin;
using HomeService.Application.Auditing;
using HomeService.Application.CompanyPortal;
using HomeService.Application.Notifications;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class AdminCompanyApplicationReviewServiceTests
{
    [Fact]
    public async Task ApproveAsync_WhenRequiredDocumentsAreApproved_PersistsCompanyAndAudit()
    {
        await using var db = CreateDbContext();
        var application = AddCompanyApplication(db);
        AddApprovedRequiredDocuments(db, application.Id);
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var result = await sut.ApproveAsync(
            application.Id,
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "unit-tests", "application-approve"),
            CancellationToken.None);

        Assert.Equal(AdminCompanyApplicationReviewStatus.Ok, result.Status);
        var savedApplication = await db.CompanyApplications.SingleAsync(item => item.Id == application.Id);
        Assert.Equal(CompanyApplicationStatus.Approved, savedApplication.Status);
        Assert.NotNull(savedApplication.CompanyId);
        Assert.True(await db.Companies.AnyAsync(company => company.Id == savedApplication.CompanyId));
        Assert.True(await db.CompanyApplicationStatusHistories.AnyAsync(history => history.CompanyApplicationId == application.Id));
        var audit = await db.AuditLogEntries.SingleAsync();
        Assert.Equal("AdminCompanyApplicationApproved", audit.Action);
        Assert.Equal(nameof(CompanyApplication), audit.EntityType);
        Assert.Equal(application.Id, audit.EntityId);
        Assert.Equal("application-approve", audit.CorrelationId);
    }

    [Fact]
    public async Task RejectAsync_WhenNoteIsProvided_PersistsNotificationAndAudit()
    {
        await using var db = CreateDbContext();
        var application = AddCompanyApplication(db);
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var result = await sut.RejectAsync(
            application.Id,
            "Dossier incoherent",
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "unit-tests", "application-reject"),
            CancellationToken.None);

        Assert.Equal(AdminCompanyApplicationReviewStatus.Ok, result.Status);
        var savedApplication = await db.CompanyApplications.SingleAsync(item => item.Id == application.Id);
        Assert.Equal(CompanyApplicationStatus.Rejected, savedApplication.Status);
        Assert.Equal("Dossier incoherent", savedApplication.ReviewNote);
        Assert.True(await db.NotificationOutboxMessages.AnyAsync());
        var audit = await db.AuditLogEntries.SingleAsync();
        Assert.Equal("AdminCompanyApplicationRejected", audit.Action);
        Assert.Equal(application.Id, audit.EntityId);
        Assert.Equal("application-reject", audit.CorrelationId);
    }

    [Fact]
    public async Task RequestMoreInformationAsync_WhenNoteIsProvided_PersistsNotificationAndAudit()
    {
        await using var db = CreateDbContext();
        var application = AddCompanyApplication(db);
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var result = await sut.RequestMoreInformationAsync(
            application.Id,
            "Merci d'ajouter le DFE",
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "unit-tests", "application-more-info"),
            CancellationToken.None);

        Assert.Equal(AdminCompanyApplicationReviewStatus.Ok, result.Status);
        var savedApplication = await db.CompanyApplications.SingleAsync(item => item.Id == application.Id);
        Assert.Equal(CompanyApplicationStatus.MoreInformationRequested, savedApplication.Status);
        Assert.Equal("Merci d'ajouter le DFE", savedApplication.ReviewNote);
        Assert.True(await db.NotificationOutboxMessages.AnyAsync());
        var audit = await db.AuditLogEntries.SingleAsync();
        Assert.Equal("AdminCompanyApplicationMoreInformationRequested", audit.Action);
        Assert.Equal(application.Id, audit.EntityId);
        Assert.Equal("application-more-info", audit.CorrelationId);
    }

    [Fact]
    public async Task ReopenAsync_WhenRejected_PersistsUnderReviewAndAudit()
    {
        await using var db = CreateDbContext();
        var application = AddCompanyApplication(db);
        application.Reject("Ancien refus", "admin");
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var result = await sut.ReopenAsync(
            application.Id,
            "Nouvelle verification",
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "unit-tests", "application-reopen"),
            CancellationToken.None);

        Assert.Equal(AdminCompanyApplicationReviewStatus.Ok, result.Status);
        var savedApplication = await db.CompanyApplications.SingleAsync(item => item.Id == application.Id);
        Assert.Equal(CompanyApplicationStatus.UnderReview, savedApplication.Status);
        Assert.Equal("Nouvelle verification", savedApplication.ReviewNote);
        var audit = await db.AuditLogEntries.SingleAsync();
        Assert.Equal("AdminCompanyApplicationReopened", audit.Action);
        Assert.Equal(application.Id, audit.EntityId);
        Assert.Equal("application-reopen", audit.CorrelationId);
    }

    private static CompanyApplication AddCompanyApplication(HomeServiceDbContext db)
    {
        var application = new CompanyApplication(
            "Wélé Services",
            null,
            "Abidjan",
            "Cocody",
            "Awa Kone",
            "contact@wele.ci",
            "+2250700000000",
            "Menage",
            4);
        db.CompanyApplications.Add(application);
        return application;
    }

    private static void AddApprovedRequiredDocuments(HomeServiceDbContext db, Guid applicationId)
    {
        foreach (var documentType in new[]
        {
            CompanyDocumentType.FiscalExistenceDeclaration,
            CompanyDocumentType.BusinessRegistration,
            CompanyDocumentType.OwnerIdentity
        })
        {
            var document = new CompanyApplicationDocument(
                applicationId,
                documentType,
                $"{documentType}.png",
                $"company-applications/{applicationId}/{documentType}.png",
                "image/png");
            document.Approve();
            db.CompanyApplicationDocuments.Add(document);
        }
    }

    private static AdminCompanyApplicationReviewService CreateService(HomeServiceDbContext db)
    {
        return new AdminCompanyApplicationReviewService(
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
