using HomeService.Application.Abstractions;
using HomeService.Infrastructure.Data;
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

        return services;
    }
}
