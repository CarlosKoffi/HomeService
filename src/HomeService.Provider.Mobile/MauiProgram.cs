using HomeService.Provider.Mobile.Services;

namespace HomeService.Provider.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>();

        builder.Services.AddSingleton(new HttpClient
        {
            BaseAddress = new Uri("https://api.wele.local/"),
            Timeout = TimeSpan.FromSeconds(12)
        });
        builder.Services.AddSingleton<ProviderMobileApiClient>();

        return builder.Build();
    }
}
