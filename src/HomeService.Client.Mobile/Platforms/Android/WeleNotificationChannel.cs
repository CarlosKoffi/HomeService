using Android.App;
using Android.Content;

namespace HomeService.Client.Mobile;

public static class WeleNotificationChannel
{
    public const string ChannelId = "wele_missions";

    public static void EnsureCreated(Context context)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return;
        }

        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        manager?.CreateNotificationChannel(new NotificationChannel(
            ChannelId,
            "Suivi des missions",
            NotificationImportance.High)
        {
            Description = "Affectations, arrivees et mises a jour de vos demandes Wele."
        });
    }
}
