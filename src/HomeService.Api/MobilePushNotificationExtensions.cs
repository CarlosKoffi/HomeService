namespace HomeService.Api;

public static class MobilePushNotificationExtensions
{
    public static IServiceCollection AddMobilePushNotifications(this IServiceCollection services)
    {
        services.AddHostedService<MobilePushNotificationHostedService>();
        return services;
    }
}
