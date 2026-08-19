using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.AppCompat.App;
using HomeService.Provider.Mobile.Services;

namespace HomeService.Provider.Mobile;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize
        | ConfigChanges.Orientation
        | ConfigChanges.UiMode
        | ConfigChanges.ScreenLayout
        | ConfigChanges.SmallestScreenSize
        | ConfigChanges.Density)]
[IntentFilter(
    [Intent.ActionView],
    Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataScheme = "wele-provider",
    DataHost = "activation")]
[IntentFilter(
    [Intent.ActionView],
    Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataScheme = "wele-provider",
    DataHost = "login")]
[IntentFilter(
    [Intent.ActionView],
    Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataScheme = "https",
    DataHost = "pro.wele.africa",
    DataPathPrefix = "/activation",
    AutoVerify = true)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        AppCompatDelegate.DefaultNightMode = AppCompatDelegate.ModeNightNo;
        CaptureDeepLinkIntent(Intent);
        base.OnCreate(savedInstanceState);
        CaptureNotificationIntent(Intent);
        WeleProviderNotificationChannel.EnsureCreated(this);

        if (OperatingSystem.IsAndroidVersionAtLeast(33)
            && CheckSelfPermission(Android.Manifest.Permission.PostNotifications) != Permission.Granted)
        {
            RequestPermissions([Android.Manifest.Permission.PostNotifications], 2001);
        }
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        Intent = intent;
        CaptureDeepLinkIntent(intent);
        CaptureNotificationIntent(intent);
        _ = ProviderDeepLinkNavigationService.TryNavigateAsync();
        _ = ProviderNotificationNavigationService.TryNavigateAsync();
    }

    protected override void OnResume()
    {
        base.OnResume();
        BottomNavigationTypography.Apply(this);
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
        ProviderNotificationNavigationService.Store(data);
    }

    private static void CaptureDeepLinkIntent(Intent? intent)
    {
        var rawUri = intent?.Data?.ToString();
        if (System.Uri.TryCreate(rawUri, UriKind.Absolute, out var uri))
        {
            ProviderDeepLinkNavigationService.Store(uri);
        }
    }
}
