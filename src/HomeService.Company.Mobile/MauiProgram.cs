using HomeService.Company.Mobile.Services;
using HomeService.Mobile.Shared;
using Microsoft.Maui.Storage;

namespace HomeService.Company.Mobile;

public static class MauiProgram
{
    private const string DefaultApiBaseUrl = "http://x295g8jkokv8bax1mijpzhpf.167.233.194.252.sslip.io/";

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
#if ANDROID || IOS || MACCATALYST
        builder.UseMauiMaps();
#endif
        builder.Services.AddSingleton(new HttpClient
        {
            BaseAddress = new Uri(GetApiBaseUrl()),
            Timeout = TimeSpan.FromSeconds(90)
        });
        builder.Services.AddSingleton<CompanySessionStore>();
        builder.Services.AddSingleton<CompanyMobileApiClient>();
        builder.Services.AddSingleton<CompanyDeviceRegistrationService>();
        builder.Services.AddSingleton<CatalogMediaResolver>();
        return builder.Build();
    }

    private static string GetApiBaseUrl()
    {
        var configured = Environment.GetEnvironmentVariable("WELE_API_BASE_URL");
        if (string.IsNullOrWhiteSpace(configured))
        {
            configured = Preferences.Default.Get("ApiBaseUrl", DefaultApiBaseUrl);
        }

        configured = configured.Trim();
        return configured.EndsWith('/') ? configured : $"{configured}/";
    }
}
