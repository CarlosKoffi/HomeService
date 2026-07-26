using HomeService.Application.Admin;
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
        await db.SaveChangesAsync();
        var sut = new AdminCompanyApplicationDocumentReviewService(
            db,
            new CompanyPortalNotificationWriter(db),
            new NotificationDeliveryPreferenceService(db));

        var result = await sut.RejectAsync(document.Id, "Photo illisible", CancellationToken.None);
        await db.SaveChangesAsync();

        Assert.Equal(AdminCompanyApplicationDocumentReviewStatus.Ok, result.Status);
        var notification = await db.CompanyPortalNotifications.SingleAsync();
        Assert.Equal("CompanyDocumentRejected", notification.Type);
        Assert.Equal("Une piece de votre dossier a ete refusee", notification.Title);
        Assert.Equal("Photo illisible", notification.Message);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }
}
