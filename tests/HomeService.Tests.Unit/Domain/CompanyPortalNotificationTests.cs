using HomeService.Domain.Entities;

namespace HomeService.Tests.Unit.Domain;

public sealed class CompanyPortalNotificationTests
{
    [Fact]
    public void Constructor_WhenDataIsValid_CreatesUnreadNotification()
    {
        var companyId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        var notification = new CompanyPortalNotification(
            companyId,
            applicationId,
            documentId,
            "CompanyDocumentRejected",
            "Piece a reprendre",
            "Le DFE est illisible.",
            "danger",
            "company-profile#documents");

        Assert.Equal(companyId, notification.CompanyId);
        Assert.Equal(applicationId, notification.CompanyApplicationId);
        Assert.Equal(documentId, notification.CompanyApplicationDocumentId);
        Assert.Equal("Piece a reprendre", notification.Title);
        Assert.False(notification.IsRead);
    }

    [Fact]
    public void MarkRead_WhenUnread_MarksNotificationRead()
    {
        var notification = new CompanyPortalNotification(
            Guid.NewGuid(),
            null,
            null,
            "CompanyApplicationMoreInformationRequested",
            "Complement demande",
            "Ajoutez votre zone d'intervention.",
            "warning");

        notification.MarkRead();

        Assert.True(notification.IsRead);
        Assert.NotNull(notification.UpdatedAt);
    }
}
