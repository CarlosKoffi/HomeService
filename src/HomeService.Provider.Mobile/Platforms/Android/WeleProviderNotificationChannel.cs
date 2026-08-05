using Android.App;
using Android.Content;

namespace HomeService.Provider.Mobile;

public static class WeleProviderNotificationChannel
{
    public const string ChannelId = "wele_provider_missions";

    public static void EnsureCreated(Context context)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return;
        }

        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        manager?.CreateNotificationChannel(new NotificationChannel(
            ChannelId,
            "Missions Wélé Pro",
            NotificationImportance.High)
        {
            Description = "Nouvelles missions, changements de statut et messages Wélé Pro."
        });
    }
}
