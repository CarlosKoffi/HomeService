using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.AppCompat.App;
using HomeService.Client.Mobile.Services;

namespace HomeService.Client.Mobile;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize
        | ConfigChanges.Orientation
        | ConfigChanges.UiMode
        | ConfigChanges.ScreenLayout
        | ConfigChanges.SmallestScreenSize
        | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        AppCompatDelegate.DefaultNightMode = AppCompatDelegate.ModeNightNo;
        base.OnCreate(savedInstanceState);
        CaptureNotificationIntent(Intent);
        WeleNotificationChannel.EnsureCreated(this);

        if (OperatingSystem.IsAndroidVersionAtLeast(33)
            && CheckSelfPermission(Android.Manifest.Permission.PostNotifications) != Permission.Granted)
        {
            RequestPermissions([Android.Manifest.Permission.PostNotifications], 1001);
        }
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        Intent = intent;
        CaptureNotificationIntent(intent);
        _ = ClientNotificationNavigationService.TryNavigateAsync();
    }

    private static void CaptureNotificationIntent(Intent? intent)
    {
        if (intent?.Extras is null) return;
        var keys = intent.Extras.KeySet();
        if (keys is null) return;
        var data = keys
            .Select(key => new { key, value = intent.Extras.Get(key)?.ToString() })
            .Where(item => !string.IsNullOrWhiteSpace(item.value))
            .ToDictionary(item => item.key, item => item.value!);
        ClientNotificationNavigationService.Store(data);
    }
}
