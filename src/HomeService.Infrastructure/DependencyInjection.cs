using HomeService.Application.Abstractions;
using HomeService.Application.Clients;
using HomeService.Application.Notifications;
using HomeService.Infrastructure.Data;
using HomeService.Infrastructure.Notifications;
using HomeService.Infrastructure.Location;
using HomeService.Infrastructure.Security;
using HomeService.Infrastructure.Payments;
using HomeService.Application.Admin;
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
            options.Enabled = string.Equals(
                configuration["FIREBASE_NOTIFICATIONS_ENABLED"] ?? configuration["Firebase:Enabled"],
                "true",
                StringComparison.OrdinalIgnoreCase);
            options.ProjectId = configuration["FIREBASE_PROJECT_ID"] ?? configuration["Firebase:ProjectId"];
            options.CredentialsJson = FirebaseCredentialsJsonResolver.Resolve(
                configuration["FIREBASE_CREDENTIALS_JSON"] ?? configuration["Firebase:CredentialsJson"],
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
        services.AddSingleton<IPayoutDataProtector, AesPayoutDataProtector>();
        services.AddSingleton<IAdminMfaDataProtector, AesAdminMfaDataProtector>();
        services.AddSingleton(new AdminFinancialSecurityOptions
        {
            DualApprovalThresholdAmount = ParsePositiveInt(
                configuration["ADMIN_FINANCIAL_DUAL_APPROVAL_THRESHOLD"],
                100_000),
            ApprovalValidityMinutes = ParsePositiveInt(
                configuration["ADMIN_FINANCIAL_APPROVAL_VALIDITY_MINUTES"],
                15)
        });
        services.AddScoped<ICompanyPayoutGateway, JekoCompanyPayoutGateway>();
        services.AddScoped<IClientPaymentGateway, JekoClientPaymentGateway>();

        return services;
    }

    private static int ParsePositiveInt(string? value, int fallback) =>
        int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
}
