using HomeService.Provider.Mobile.Services;
using Microsoft.Maui.Storage;

namespace HomeService.Provider.Mobile;

public static class MauiProgram
{
    private const string ApiBaseUrlPreferenceKey = "ApiBaseUrl";
    private const string DefaultApiBaseUrl = "http://x295g8jkokv8bax1mijpzhpf.167.233.194.252.sslip.io/";

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>();

        builder.Services.AddSingleton(new HttpClient
        {
            BaseAddress = new Uri(GetApiBaseUrl()),
            Timeout = TimeSpan.FromSeconds(12)
        });
        builder.Services.AddSingleton<ProviderMobileApiClient>();

        return builder.Build();
    }

    private static string GetApiBaseUrl()
    {
        var configuredUrl = Environment.GetEnvironmentVariable("WELE_API_BASE_URL");
        if (string.IsNullOrWhiteSpace(configuredUrl))
        {
            configuredUrl = Preferences.Default.Get(ApiBaseUrlPreferenceKey, DefaultApiBaseUrl);
        }

        configuredUrl = configuredUrl.Trim();
        return configuredUrl.EndsWith("/", StringComparison.Ordinal)
            ? configuredUrl
            : $"{configuredUrl}/";
    }
}
