using Android.App;
using Android.Content;

namespace HomeService.Company.Mobile;

public static class WeleCompanyNotificationChannel
{
    public const string ChannelId = "wele_company_operations";

    public static void EnsureCreated(Context context)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return;
        }

        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        manager?.CreateNotificationChannel(new NotificationChannel(
            ChannelId,
            "Opérations wélé Entreprise",
            NotificationImportance.High)
        {
            Description = "Missions à accepter, prestataires à affecter et actions urgentes."
        });
    }
}
