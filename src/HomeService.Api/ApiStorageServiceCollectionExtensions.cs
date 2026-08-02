namespace HomeService.Api;

public static class ApiStorageServiceCollectionExtensions
{
    public static IServiceCollection AddApiStorageServices(this IServiceCollection services)
    {
        services.AddSingleton<CompanyApplicationUploadService>();
        services.AddSingleton<CompanyProviderUploadService>();
        services.AddSingleton<CmsMediaUploadService>();
        services.AddSingleton<ClientMissionPhotoUploadService>();
        services.AddSingleton<ClientProfilePhotoUploadService>();

        return services;
    }
}
