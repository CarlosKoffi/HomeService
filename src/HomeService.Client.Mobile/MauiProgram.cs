using HomeService.Client.Mobile.Services;

namespace HomeService.Client.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
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

        return builder.Build();
    }
}
