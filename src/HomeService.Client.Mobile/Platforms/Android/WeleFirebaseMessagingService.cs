using Android.App;
using Android.Content;
using AndroidX.Core.App;
using Firebase.Messaging;
using HomeService.Client.Mobile.Services;

namespace HomeService.Client.Mobile;

[Service(Exported = false)]
[IntentFilter(["com.google.firebase.MESSAGING_EVENT"])]
public sealed class WeleFirebaseMessagingService : FirebaseMessagingService
{
    public const string ChannelId = WeleNotificationChannel.ChannelId;

    public override void OnNewToken(string token)
    {
        base.OnNewToken(token);
        ClientDeviceRegistrationService.StoreFirebaseToken(token);
        _ = RegisterTokenAsync();
    }

    public override void OnMessageReceived(RemoteMessage message)
    {
        base.OnMessageReceived(message);
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                MobileServiceLocator.GetRequiredService<ClientNotificationState>().Increment();
            }
            catch (InvalidOperationException)
            {
                // The native notification remains available while MAUI is still starting.
            }
        });
        WeleNotificationChannel.EnsureCreated(this);

        var title = message.GetNotification()?.Title
            ?? ReadData(message, "title")
            ?? "Wélé";
        var body = message.GetNotification()?.Body
            ?? ReadData(message, "body")
            ?? "Votre demande a été mise à jour.";

        var launchIntent = PackageManager?.GetLaunchIntentForPackage(PackageName!);
        launchIntent?.AddFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
        foreach (var item in message.Data)
        {
            launchIntent?.PutExtra(item.Key, item.Value);
        }
        var pendingIntent = PendingIntent.GetActivity(
            this,
            message.MessageId?.GetHashCode() ?? 0,
            launchIntent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var notification = new NotificationCompat.Builder(this, ChannelId)
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
    {
        return message.Data.TryGetValue(key, out var value) ? value : null;
    }

    private static async Task RegisterTokenAsync()
    {
        try
        {
            await MobileServiceLocator
                .GetRequiredService<ClientDeviceRegistrationService>()
                .RegisterCurrentDeviceAsync();
        }
        catch (Exception)
        {
            // The persisted token is retried when the application resumes.
        }
    }
}
