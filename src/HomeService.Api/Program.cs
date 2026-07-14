using HomeService.Application.Abstractions;
using HomeService.Api;
using HomeService.Api.Auditing;
using HomeService.Api.Endpoints;
using HomeService.Contracts.Branding;
using HomeService.Contracts.Companies;
using HomeService.Contracts.CompanyPortal;
using HomeService.Contracts.Notifications;
using HomeService.Contracts.ProviderPortal;
using HomeService.Contracts.Monitoring;
using HomeService.Application.ProviderPortal;
using HomeService.Application.Branding;
using HomeService.Application.Companies;
using HomeService.Application.Admin;
using HomeService.Application.CompanyPortal;
using HomeService.Application.Security;
using HomeService.Application.Notifications;
using HomeService.Application.Auditing;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.RegularExpressions;

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
builder.Services.AddSingleton<CompanyApplicationUploadService>();
builder.Services.AddSingleton<CompanyProviderUploadService>();
builder.Services.AddScoped<ProviderMissionWorkflowService>();
builder.Services.AddScoped<CompanyApplicationRegistrationService>();
builder.Services.AddScoped<CompanyPortalAuthService>();
builder.Services.AddScoped<CompanyActivationPreviewService>();
builder.Services.AddScoped<CompanyActivationLinkGenerationService>();
builder.Services.AddScoped<CompanyActivationPasswordService>();
builder.Services.AddScoped<CompanyComplianceDocumentService>();
builder.Services.AddScoped<CompanyEmployeeInvitationService>();
builder.Services.AddScoped<CompanyEmployeeManagementService>();
builder.Services.AddScoped<CompanyInterimCandidateService>();
builder.Services.AddScoped<CompanyPortalQueryService>();
builder.Services.AddScoped<ProviderSelfRegistrationService>();
builder.Services.AddScoped<ProviderOnboardingService>();
builder.Services.AddScoped<ProviderPortalAuthService>();
builder.Services.AddScoped<AdminConfigurationService>();
builder.Services.AddScoped<AdminQueryService>();
builder.Services.AddScoped<AdminCmsQueryService>();
builder.Services.AddScoped<AdminCompanyApplicationReviewService>();
builder.Services.AddScoped<AdminCompanyApplicationDocumentReviewService>();

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
