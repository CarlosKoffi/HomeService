using Android.App;
using Android.Content;
using AndroidX.Core.App;
using Firebase.Messaging;
using HomeService.Provider.Mobile.Services;

namespace HomeService.Provider.Mobile;

[Service(Exported = false)]
[IntentFilter(["com.google.firebase.MESSAGING_EVENT"])]
public sealed class WeleProviderFirebaseMessagingService : FirebaseMessagingService
{
    public override void OnNewToken(string token)
    {
        base.OnNewToken(token);
        ProviderDeviceRegistrationService.StoreFirebaseToken(token);
        _ = RegisterTokenAsync();
    }

    public override void OnMessageReceived(RemoteMessage message)
    {
        base.OnMessageReceived(message);
        WeleProviderNotificationChannel.EnsureCreated(this);

        var title = message.GetNotification()?.Title
            ?? ReadData(message, "title")
            ?? "Wélé Pro";
        var body = message.GetNotification()?.Body
            ?? ReadData(message, "body")
            ?? "Une mission a été mise à jour.";

        var launchIntent = PackageManager?.GetLaunchIntentForPackage(PackageName!);
        launchIntent?.AddFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
        var pendingIntent = PendingIntent.GetActivity(
            this,
            message.MessageId?.GetHashCode() ?? 0,
            launchIntent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var notification = new NotificationCompat.Builder(this, WeleProviderNotificationChannel.ChannelId)
            .SetSmallIcon(Resource.Drawable.notification_icon)
            .SetContentTitle(title)
            .SetContentText(body)
            .SetStyle(new NotificationCompat.BigTextStyle().BigText(body))
            .SetPriority(NotificationCompat.PriorityHigh)
            .SetAutoCancel(true)
            .SetContentIntent(pendingIntent)
            .Build();

        NotificationManagerCompat.From(this)
            .Notify(message.MessageId?.GetHashCode() ?? DateTime.UtcNow.Millisecond, notification);
    }

    private static string? ReadData(RemoteMessage message, string key)
        => message.Data.TryGetValue(key, out var value) ? value : null;

    private static async Task RegisterTokenAsync()
    {
        try
        {
            var service = IPlatformApplication.Current?.Services.GetService<ProviderDeviceRegistrationService>();
            if (service is not null)
            {
                await service.RegisterCurrentDeviceAsync();
            }
        }
        catch
        {
            // Persisted token is retried at the next authenticated app resume.
        }
    }
}
