using HomeService.Application.Admin;
using HomeService.Application.Clients;
using HomeService.Application.Cms;
using HomeService.Application.Companies;
using HomeService.Application.CompanyPortal;
using HomeService.Application.Contact;
using HomeService.Application.Missions;
using HomeService.Application.Notifications;
using HomeService.Application.ProviderPortal;
using Microsoft.Extensions.DependencyInjection;

namespace HomeService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ProviderMissionWorkflowService>();
        services.AddScoped<ClientMissionRequestService>();
        services.AddScoped<ClientMissionPreparationService>();
        services.AddScoped<ClientMissionConfirmationService>();
        services.AddScoped<ClientMissionCancellationService>();
        services.AddScoped<ClientMissionCompletionValidationService>();
        services.AddScoped<ClientMissionStatusService>();
        services.AddScoped<ClientMissionPaymentMethodService>();
        services.AddScoped<ClientAuthService>();
        services.AddScoped<ClientProfileService>();
        services.AddScoped<ClientCatalogSearchService>();
        services.AddScoped<ClientMissionListService>();
        services.AddScoped<ClientMissionChatService>();
        services.AddScoped<ClientMissionScreenService>();
        services.AddScoped<ClientNotificationInboxService>();
        services.AddScoped<ClientHomeService>();
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
        services.AddScoped<CompanyMissionOfferService>();
        services.AddScoped<CompanyPortalDashboardService>();
        services.AddScoped<CompanyPortalNotificationService>();
        services.AddScoped<CompanyPortalNotificationWriter>();
        services.AddScoped<MobileDeviceTokenService>();
        services.AddScoped<MobilePushNotificationQueueService>();
        services.AddScoped<CustomerMissionProgressNotificationService>();
        services.AddScoped<MobilePushOutboxDispatcherService>();
        services.AddScoped<NotificationDeliveryPreferenceService>();
        services.AddScoped<NotificationCatalogSeeder>();
        services.AddScoped<NotificationTemplateService>();
        services.AddScoped<CompanyPortalProfileManagementService>();
        services.AddScoped<CompanyPortalQueryService>();
        services.AddScoped<MissionDispatchScoringService>();
        services.AddScoped<MissionDispatchService>();
        services.AddScoped<MissionCancellationWorkflowService>();
        services.AddScoped<MissionPaymentMilestoneService>();
        services.AddScoped<MissionAdditionalQuoteWorkflowService>();
        services.AddScoped<ProviderAssignmentExpirationService>();
        services.AddScoped<ProviderSelfRegistrationService>();
        services.AddScoped<ProviderOnboardingService>();
        services.AddScoped<ProviderPortalAuthService>();
        services.AddScoped<ProviderMobileProfileService>();
        services.AddScoped<ProviderMobileProfileUpdateService>();
        services.AddScoped<ProviderMobileMissionDetailService>();
        services.AddScoped<ProviderMissionChatService>();
        services.AddScoped<ProviderMissionNotificationService>();
        services.AddScoped<CompanyHomeCmsQueryService>();
        services.AddScoped<ProviderHomeCmsQueryService>();
        services.AddScoped<ContactRequestService>();
        services.AddScoped<AdminConfigurationService>();
        services.AddScoped<AdminAuthService>();
        services.AddScoped<AdminQueryService>();
        services.AddScoped<AdminClientQueryService>();
        services.AddScoped<AdminAccessControlService>();
        services.AddScoped<AdminCmsQueryService>();
        services.AddScoped<AdminCmsContentManagementService>();
        services.AddScoped<AdminCompanyApplicationReviewService>();
        services.AddScoped<AdminCompanyApplicationDocumentReviewService>();
        services.AddScoped<AdminCompanyNotificationService>();
        services.AddScoped<AdminCompanyOperationsService>();
        services.AddScoped<AdminServiceCatalogManagementService>();
        services.AddScoped<AdminCompanyServiceProposalService>();
        services.AddScoped<AdminNotificationDeliveryRuleService>();
        services.AddScoped<AdminNotificationTemplateService>();
        services.AddScoped<AdminNotificationService>();
        services.AddScoped<AdminTranslationService>();
        services.AddScoped<AdminMissionSettingsService>();
        services.AddScoped<AdminMissionOperationsService>();
        services.AddScoped<AdminMissionDisputeService>();
        services.AddScoped<AdminProviderOperationsService>();
        services.AddScoped<AdminServiceCatalogInsightsService>();

        return services;
    }
}
