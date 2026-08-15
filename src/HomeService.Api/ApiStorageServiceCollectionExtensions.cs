using Microsoft.AspNetCore.Http.Features;

namespace HomeService.Api;

public static class ApiStorageServiceCollectionExtensions
{
    public static IServiceCollection AddApiStorageServices(this IServiceCollection services)
    {
        services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = 30L * 1024 * 1024;
        });

        services.AddSingleton<IApiObjectStorage, ApiObjectStorage>();
        services.AddHostedService<R2PublicAssetSeeder>();
        services.AddHostedService<R2HistoricalAssetSeeder>();
        services.AddSingleton<CompanyApplicationUploadService>();
        services.AddSingleton<CompanyProviderUploadService>();
        services.AddSingleton<CmsMediaUploadService>();
        services.AddSingleton<ClientMissionPhotoUploadService>();
        services.AddSingleton<ClientProfilePhotoUploadService>();
        services.AddSingleton<BusinessClientDocumentUploadService>();

        return services;
    }
}
