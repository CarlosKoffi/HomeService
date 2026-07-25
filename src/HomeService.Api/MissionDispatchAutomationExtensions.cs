namespace HomeService.Api;

public static class MissionDispatchAutomationExtensions
{
    public static IServiceCollection AddMissionDispatchAutomation(this IServiceCollection services)
    {
        services.AddHostedService<MissionDispatchAutomationHostedService>();
        return services;
    }
}
