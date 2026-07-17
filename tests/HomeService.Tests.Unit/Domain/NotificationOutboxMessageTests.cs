using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Tests.Unit.Domain;

public sealed class NotificationOutboxMessageTests
{
    [Fact]
    public void Retry_WhenFailed_ReturnsToPendingAndClearsFailure()
    {
        var notification = CreateNotification();
        notification.MarkFailed("Provider unavailable");

        notification.Retry();

        Assert.Equal(NotificationStatus.Pending, notification.Status);
        Assert.Null(notification.FailureReason);
    }

    [Fact]
    public void Cancel_WhenPending_MarksNotificationCancelled()
    {
        var notification = CreateNotification();

        notification.Cancel("Doublon");

        Assert.Equal(NotificationStatus.Cancelled, notification.Status);
        Assert.Equal("Doublon", notification.FailureReason);
    }

    [Fact]
    public void Cancel_WhenSent_Throws()
    {
        var notification = CreateNotification();
        notification.MarkSent();

        Assert.Throws<InvalidOperationException>(() => notification.Cancel("Erreur"));
    }

    [Fact]
    public void Retry_WhenSent_Throws()
    {
        var notification = CreateNotification();
        notification.MarkSent();

        Assert.Throws<InvalidOperationException>(notification.Retry);
    }

    private static NotificationOutboxMessage CreateNotification()
    {
        return new NotificationOutboxMessage(
            NotificationChannel.Email,
            "contact@kaza.ci",
            "Validation",
            "Votre dossier avance.",
            "CompanyApplication",
            Guid.NewGuid());
    }
}
