using HomeService.Application.Admin;
using HomeService.Application.Cms;
using HomeService.Application.Companies;
using HomeService.Application.CompanyPortal;
using HomeService.Application.ProviderPortal;
using Microsoft.Extensions.DependencyInjection;

namespace HomeService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ProviderMissionWorkflowService>();
        services.AddScoped<CompanyApplicationRegistrationService>();
        services.AddScoped<CompanyPortalAuthService>();
        services.AddScoped<CompanyActivationPreviewService>();
        services.AddScoped<CompanyActivationLinkGenerationService>();
        services.AddScoped<CompanyActivationPasswordService>();
        services.AddScoped<CompanyComplianceDocumentService>();
        services.AddScoped<CompanyEmployeeInvitationService>();
        services.AddScoped<CompanyEmployeeManagementService>();
        services.AddScoped<CompanyInterimCandidateService>();
        services.AddScoped<CompanyMissionAssignmentService>();
        services.AddScoped<CompanyPortalDashboardService>();
        services.AddScoped<CompanyPortalProfileManagementService>();
        services.AddScoped<CompanyPortalQueryService>();
        services.AddScoped<ProviderSelfRegistrationService>();
        services.AddScoped<ProviderOnboardingService>();
        services.AddScoped<ProviderPortalAuthService>();
        services.AddScoped<CompanyHomeCmsQueryService>();
        services.AddScoped<AdminConfigurationService>();
        services.AddScoped<AdminQueryService>();
        services.AddScoped<AdminAccessControlService>();
        services.AddScoped<AdminCmsQueryService>();
        services.AddScoped<AdminCompanyApplicationReviewService>();
        services.AddScoped<AdminCompanyApplicationDocumentReviewService>();
        services.AddScoped<AdminNotificationService>();

        return services;
    }
}
