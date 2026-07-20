using HomeService.Application.CompanyPortal;
using HomeService.Domain.Entities;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class CompanyPortalNotificationServiceTests
{
    [Fact]
    public async Task MarkReadAsync_MarksOnlyTheRequestedCompanyNotification()
    {
        await using var db = CreateDbContext();
        var company = CreateApprovedCompany();
        var otherCompany = CreateApprovedCompany();
        var notification = CreateNotification(company.Id, "Piece a revoir");
        var otherNotification = CreateNotification(otherCompany.Id, "Autre entreprise");
        db.Companies.AddRange(company, otherCompany);
        db.CompanyPortalNotifications.AddRange(notification, otherNotification);
        await db.SaveChangesAsync();

        var result = await new CompanyPortalNotificationService(db).MarkReadAsync(
            company.Id,
            notification.Id,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.UpdatedCount);
        Assert.True(notification.IsRead);
        Assert.False(otherNotification.IsRead);
    }

    [Fact]
    public async Task MarkReadAsync_ReturnsNotFoundWhenNotificationBelongsToAnotherCompany()
    {
        await using var db = CreateDbContext();
        var company = CreateApprovedCompany();
        var otherCompany = CreateApprovedCompany();
        var notification = CreateNotification(otherCompany.Id, "Autre entreprise");
        db.Companies.AddRange(company, otherCompany);
        db.CompanyPortalNotifications.Add(notification);
        await db.SaveChangesAsync();

        var result = await new CompanyPortalNotificationService(db).MarkReadAsync(
            company.Id,
            notification.Id,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, result.UpdatedCount);
        Assert.False(notification.IsRead);
    }

    [Fact]
    public async Task ListAsync_ReturnsUnreadCountForCompanyOnly()
    {
        await using var db = CreateDbContext();
        var company = CreateApprovedCompany();
        var otherCompany = CreateApprovedCompany();
        var readNotification = CreateNotification(company.Id, "Document valide");
        readNotification.MarkRead();
        db.Companies.AddRange(company, otherCompany);
        db.CompanyPortalNotifications.AddRange(
            CreateNotification(company.Id, "Piece refusee"),
            readNotification,
            CreateNotification(otherCompany.Id, "Autre entreprise"));
        await db.SaveChangesAsync();

        var result = await new CompanyPortalNotificationService(db).ListAsync(company.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.Equal(1, result.Response!.UnreadCount);
        Assert.Equal(2, result.Response.Notifications.Count);
        Assert.Contains(result.Response.Notifications, item => item.Title == "Piece refusee");
        Assert.Contains(result.Response.Notifications, item => item.Title == "Document valide");
        Assert.DoesNotContain(result.Response.Notifications, item => item.Title == "Autre entreprise");
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }

    private static Company CreateApprovedCompany()
    {
        var company = new Company("CI Home Service", "+2250700000000", "contact@example.ci");
        company.Approve();
        return company;
    }

    private static CompanyPortalNotification CreateNotification(Guid companyId, string title)
        => new(
            companyId,
            companyApplicationId: null,
            companyApplicationDocumentId: null,
            type: "CompanyDocumentReview",
            title,
            "Une action est disponible sur votre portail.",
            "warning",
            "company-profile");
}
