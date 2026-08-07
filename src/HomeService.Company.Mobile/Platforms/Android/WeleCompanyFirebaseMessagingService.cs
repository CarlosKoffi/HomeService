using Android.App;
using Android.Content;
using AndroidX.Core.App;
using Firebase.Messaging;
using HomeService.Company.Mobile.Services;

namespace HomeService.Company.Mobile;

[Service(Exported = false)]
[IntentFilter(["com.google.firebase.MESSAGING_EVENT"])]
public sealed class WeleCompanyFirebaseMessagingService : FirebaseMessagingService
{
    public override void OnNewToken(string token)
    {
        base.OnNewToken(token);
        CompanyDeviceRegistrationService.StoreFirebaseToken(token);
        _ = RegisterTokenAsync();
    }

    public override void OnMessageReceived(RemoteMessage message)
    {
        base.OnMessageReceived(message);
        WeleCompanyNotificationChannel.EnsureCreated(this);

        var title = message.GetNotification()?.Title ?? ReadData(message, "title") ?? "Wélé Entreprise";
        var body = message.GetNotification()?.Body ?? ReadData(message, "body") ?? "Une action attend votre entreprise.";
        var launchIntent = PackageManager?.GetLaunchIntentForPackage(PackageName!);
        launchIntent?.AddFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
        foreach (var item in message.Data)
        {
            launchIntent?.PutExtra(item.Key, item.Value);
        }

        var requestCode = message.MessageId?.GetHashCode() ?? DateTime.UtcNow.Millisecond;
        var pendingIntent = PendingIntent.GetActivity(
            this,
            requestCode,
            launchIntent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        var notification = new NotificationCompat.Builder(this, WeleCompanyNotificationChannel.ChannelId)
            .SetSmallIcon(Resource.Drawable.notification_icon)
            .SetContentTitle(title)
            .SetContentText(body)
            .SetStyle(new NotificationCompat.BigTextStyle().BigText(body))
            .SetPriority(NotificationCompat.PriorityHigh)
            .SetAutoCancel(true)
            .SetContentIntent(pendingIntent)
            .Build();
        NotificationManagerCompat.From(this).Notify(requestCode, notification);
    }

    private static string? ReadData(RemoteMessage message, string key)
        => message.Data.TryGetValue(key, out var value) ? value : null;

    private static async Task RegisterTokenAsync()
    {
        try
        {
            var service = IPlatformApplication.Current?.Services.GetService<CompanyDeviceRegistrationService>();
            if (service is not null) await service.RegisterCurrentDeviceAsync();
        }
        catch
        {
            // The persisted token is retried on the next authenticated resume.
        }
    }
}
