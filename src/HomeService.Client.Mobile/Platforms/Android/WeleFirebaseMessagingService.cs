using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using Firebase.Messaging;
using HomeService.Client.Mobile.Services;

namespace HomeService.Client.Mobile;

[Service(Exported = false)]
[IntentFilter(["com.google.firebase.MESSAGING_EVENT"])]
public sealed class WeleFirebaseMessagingService : FirebaseMessagingService
{
    public const string ChannelId = "wele_missions";

    public override void OnNewToken(string token)
    {
        base.OnNewToken(token);
        ClientDeviceRegistrationService.StoreFirebaseToken(token);
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
        EnsureNotificationChannel();

        var title = message.GetNotification()?.Title
            ?? ReadData(message, "title")
            ?? "Wélé";
        var body = message.GetNotification()?.Body
            ?? ReadData(message, "body")
            ?? "Votre demande a été mise à jour.";

        var launchIntent = PackageManager?.GetLaunchIntentForPackage(PackageName!);
        launchIntent?.AddFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
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

    private void EnsureNotificationChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
        {
            return;
        }

        var manager = (NotificationManager?)GetSystemService(NotificationService);
        manager?.CreateNotificationChannel(new NotificationChannel(
            ChannelId,
            "Suivi des missions",
            NotificationImportance.High)
        {
            Description = "Affectations, arrivées et mises à jour de vos demandes Wélé."
        });
    }

    private static string? ReadData(RemoteMessage message, string key)
    {
        return message.Data.TryGetValue(key, out var value) ? value : null;
    }
}
