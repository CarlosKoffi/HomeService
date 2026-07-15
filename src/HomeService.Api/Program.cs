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
        Title = "HomeService API",
        Version = "v1",
        Description = "API centrale pour la plateforme HomeService: services, entreprises, validation admin et futurs parcours client/prestataire."
    });
});
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddSingleton<CompanyApplicationUploadService>();
builder.Services.AddSingleton<CompanyProviderUploadService>();

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Services);

app.UseSiteAccessGate();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.DocumentTitle = "HomeService API";
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "HomeService API v1");
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
