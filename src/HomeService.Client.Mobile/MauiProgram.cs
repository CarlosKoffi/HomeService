using HomeService.Client.Mobile.Services;
#if ANDROID
using Microsoft.Maui.Handlers;
#endif
#if WINDOWS
using Microsoft.Maui.Handlers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
#endif

namespace HomeService.Client.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
#if ANDROID
        EntryHandler.Mapper.AppendToMapping("BorderlessEntry", (handler, _) =>
        {
            handler.PlatformView.Background = null;
            handler.PlatformView.SetPadding(0, 0, 0, 0);
        });
#endif
#if WINDOWS
        EntryHandler.Mapper.AppendToMapping("BorderlessEntry", (handler, _) =>
        {
            handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
            handler.PlatformView.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
        });
#endif

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>();
#if ANDROID || IOS || MACCATALYST
        builder.UseMauiMaps();
#endif
        builder
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("PlusJakartaSans-Variable.ttf", "PlusJakartaSans");
            });
        var apiBaseUrl = Environment.GetEnvironmentVariable("WELE_API_BASE_URL")
            ?? Preferences.Default.Get("ApiBaseUrl", "http://x295g8jkokv8bax1mijpzhpf.167.233.194.252.sslip.io/");

        builder.Services.AddSingleton<ClientSessionStore>();
        builder.Services.AddSingleton(serviceProvider =>
        {
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri(apiBaseUrl, UriKind.Absolute),
                Timeout = TimeSpan.FromSeconds(20)
            };

            return new ClientMobileApiClient(httpClient, serviceProvider.GetRequiredService<ClientSessionStore>());
        });
        builder.Services.AddSingleton<ClientDeviceRegistrationService>();
        builder.Services.AddSingleton<ClientNotificationState>();
        builder.Services.AddSingleton<MissionReviewDraftStore>();

        return builder.Build();
    }
}
