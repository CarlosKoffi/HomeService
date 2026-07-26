using HomeService.Application.Abstractions;
using HomeService.Application.Notifications;
using HomeService.Infrastructure.Data;
using HomeService.Infrastructure.Notifications;
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
            options.CredentialsJson = configuration["FIREBASE_CREDENTIALS_JSON"];
        });
        services.AddSingleton<HttpClient>();
        services.AddScoped<IMobilePushSender, FirebaseCloudMessagingSender>();

        return services;
    }
}
