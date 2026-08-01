using HomeService.Application.Abstractions;
using HomeService.Application.Clients;
using HomeService.Application.Notifications;
using HomeService.Infrastructure.Data;
using HomeService.Infrastructure.Notifications;
using HomeService.Infrastructure.Location;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HomeService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = DatabaseConnectionStringResolver.Resolve(
            configuration.GetConnectionString("DefaultConnection"),
            configuration["DATABASE_URL"],
            configuration["POSTGRES_URL"]);

        services.AddDbContext<HomeServiceDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<HomeServiceDbContext>());
        services.Configure<FirebaseOptions>(options =>
        {
            options.Enabled = string.Equals(configuration["FIREBASE_NOTIFICATIONS_ENABLED"], "true", StringComparison.OrdinalIgnoreCase);
            options.ProjectId = configuration["FIREBASE_PROJECT_ID"];
            options.CredentialsJson = FirebaseCredentialsJsonResolver.Resolve(
                configuration["FIREBASE_CREDENTIALS_JSON"],
                configuration["FIREBASE_CREDENTIALS_BASE64"],
                configuration["FIREBASE_CREDENTIALS_JSON_BASE64"]);
        });
        services.AddSingleton<HttpClient>();
        services.Configure<GooglePlacesOptions>(options =>
        {
            options.Enabled = string.Equals(
                configuration["GOOGLE_PLACES_ENABLED"] ?? configuration["GooglePlaces:Enabled"],
                "true",
                StringComparison.OrdinalIgnoreCase);
            options.ApiKey = configuration["GOOGLE_PLACES_API_KEY"] ?? configuration["GooglePlaces:ApiKey"];
        });
        services.AddScoped<IAddressAutocompleteService, GooglePlacesAddressAutocompleteService>();
        services.AddScoped<IMobilePushSender, FirebaseCloudMessagingSender>();

        return services;
    }
}
