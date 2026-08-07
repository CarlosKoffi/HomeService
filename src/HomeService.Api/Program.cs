using HomeService.Api;
using HomeService.Api.Endpoints;
using HomeService.Application;
using HomeService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "wélé API",
        Version = "v1",
        Description = "API centrale pour la plateforme wélé: services, entreprises, validation admin et parcours client/prestataire."
    });
});
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddMissionDispatchAutomation();
builder.Services.AddMobilePushNotifications();
builder.Services.AddApiStorageServices();
builder.Services.AddAuthenticationRateLimiting();

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Services);

app.UseSiteAccessGate();
app.UseR2PublicAssetDelivery();
app.UseStaticFiles();
app.UseRateLimiter();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.DocumentTitle = "wélé API";
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "wélé API v1");
    options.RoutePrefix = "swagger";
});

if (string.Equals(app.Configuration["FORCE_HTTPS_REDIRECT"], "true", StringComparison.OrdinalIgnoreCase))
{
    app.UseHttpsRedirection();
}

app.MapPublicEndpoints();
app.MapProviderOnboardingEndpoints();
app.MapCompanyActivationEndpoints();
app.MapCompanyPortalEndpoints();

app.MapProviderPortalEndpoints();

app.MapAdminEndpoints();

app.Run();

public partial class Program;
