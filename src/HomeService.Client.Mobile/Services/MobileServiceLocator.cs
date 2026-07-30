using Microsoft.Extensions.DependencyInjection;

namespace HomeService.Client.Mobile.Services;

internal static class MobileServiceLocator
{
    public static T GetRequiredService<T>()
        where T : notnull
    {
        var application = IPlatformApplication.Current
            ?? throw new InvalidOperationException("Les services de l'application ne sont pas encore disponibles.");

        return application.Services.GetRequiredService<T>();
    }
}
