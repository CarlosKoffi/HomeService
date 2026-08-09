using HomeService.Application.Abstractions;
using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Infrastructure.Data;

public sealed class HomeServiceDbContext(DbContextOptions<HomeServiceDbContext> options)
    : DbContext(options), IAppDbContext
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyPortalUser> CompanyPortalUsers => Set<CompanyPortalUser>();
    public DbSet<CompanyPortalSession> CompanyPortalSessions => Set<CompanyPortalSession>();
    public DbSet<CompanyPortalActivity> CompanyPortalActivities => Set<CompanyPortalActivity>();
    public DbSet<CompanyPortalNotification> CompanyPortalNotifications => Set<CompanyPortalNotification>();
    public DbSet<CompanyApplication> CompanyApplications => Set<CompanyApplication>();
    public DbSet<CompanyApplicationDocument> CompanyApplicationDocuments => Set<CompanyApplicationDocument>();
    public DbSet<CompanyApplicationService> CompanyApplicationServices => Set<CompanyApplicationService>();
    public DbSet<CompanyApplicationStatusHistory> CompanyApplicationStatusHistories => Set<CompanyApplicationStatusHistory>();
    public DbSet<CompanyActivationToken> CompanyActivationTokens => Set<CompanyActivationToken>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<ServicePrestation> ServicePrestations => Set<ServicePrestation>();
    public DbSet<ServiceOption> ServiceOptions => Set<ServiceOption>();
    public DbSet<ProviderProfile> Providers => Set<ProviderProfile>();
    public DbSet<ProviderInvitation> ProviderInvitations => Set<ProviderInvitation>();
    public DbSet<ProviderAffiliationRequest> ProviderAffiliationRequests => Set<ProviderAffiliationRequest>();
    public DbSet<ProviderCandidateService> ProviderCandidateServices => Set<ProviderCandidateService>();
    public DbSet<ProviderPortalSession> ProviderPortalSessions => Set<ProviderPortalSession>();
    public DbSet<ProviderDocument> ProviderDocuments => Set<ProviderDocument>();
    public DbSet<ProviderService> ProviderServices => Set<ProviderService>();
    public DbSet<ProviderServicePrestation> ProviderServicePrestations => Set<ProviderServicePrestation>();
    public DbSet<ProviderServicePortfolioItem> ProviderServicePortfolioItems => Set<ProviderServicePortfolioItem>();
    public DbSet<ProviderPrestationQualification> ProviderPrestationQualifications => Set<ProviderPrestationQualification>();
    public DbSet<ProviderQualitySummary> ProviderQualitySummaries => Set<ProviderQualitySummary>();
    public DbSet<CompanyQualitySummary> CompanyQualitySummaries => Set<CompanyQualitySummary>();
    public DbSet<QualityChecklistTemplate> QualityChecklistTemplates => Set<QualityChecklistTemplate>();
    public DbSet<QualityChecklistItem> QualityChecklistItems => Set<QualityChecklistItem>();
    public DbSet<CustomerProfile> Customers => Set<CustomerProfile>();
    public DbSet<CustomerSession> CustomerSessions => Set<CustomerSession>();
    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();
    public DbSet<CustomerPaymentMethod> CustomerPaymentMethods => Set<CustomerPaymentMethod>();
    public DbSet<PaymentProvider> PaymentProviders => Set<PaymentProvider>();
    public DbSet<Mission> Missions => Set<Mission>();
    public DbSet<MissionAttachment> MissionAttachments => Set<MissionAttachment>();
    public DbSet<MissionAdditionalQuote> MissionAdditionalQuotes => Set<MissionAdditionalQuote>();
    public DbSet<MissionFinancialBreakdown> MissionFinancialBreakdowns => Set<MissionFinancialBreakdown>();
    public DbSet<MissionPaymentMilestone> MissionPaymentMilestones => Set<MissionPaymentMilestone>();
    public DbSet<MissionReview> MissionReviews => Set<MissionReview>();
    public DbSet<MissionQualityControl> MissionQualityControls => Set<MissionQualityControl>();
    public DbSet<MissionQualityItem> MissionQualityItems => Set<MissionQualityItem>();
    public DbSet<MissionQualityAudit> MissionQualityAudits => Set<MissionQualityAudit>();
    public DbSet<MissionDispute> MissionDisputes => Set<MissionDispute>();
    public DbSet<MissionStatusHistory> MissionStatusHistories => Set<MissionStatusHistory>();
    public DbSet<MissionDispatchOffer> MissionDispatchOffers => Set<MissionDispatchOffer>();
    public DbSet<CommissionRule> CommissionRules => Set<CommissionRule>();
    public DbSet<MissionWorkflowSetting> MissionWorkflowSettings => Set<MissionWorkflowSetting>();
    public DbSet<ProviderMissionAssignment> ProviderMissionAssignments => Set<ProviderMissionAssignment>();
    public DbSet<MissionConversation> MissionConversations => Set<MissionConversation>();
    public DbSet<MissionMessage> MissionMessages => Set<MissionMessage>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<CountryBranding> CountryBrandings => Set<CountryBranding>();
    public DbSet<Language> Languages => Set<Language>();
    public DbSet<TranslationKey> TranslationKeys => Set<TranslationKey>();
    public DbSet<TranslationValue> TranslationValues => Set<TranslationValue>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<AdminRole> AdminRoles => Set<AdminRole>();
    public DbSet<AdminModule> AdminModules => Set<AdminModule>();
    public DbSet<AdminRolePermission> AdminRolePermissions => Set<AdminRolePermission>();
    public DbSet<AdminUserRole> AdminUserRoles => Set<AdminUserRole>();
    public DbSet<AdminSession> AdminSessions => Set<AdminSession>();
    public DbSet<NotificationOutboxMessage> NotificationOutboxMessages => Set<NotificationOutboxMessage>();
    public DbSet<NotificationDeliveryRule> NotificationDeliveryRules => Set<NotificationDeliveryRule>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<MobileDeviceToken> MobileDeviceTokens => Set<MobileDeviceToken>();
    public DbSet<ContactRequest> ContactRequests => Set<ContactRequest>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
    public DbSet<CmsSite> CmsSites => Set<CmsSite>();
    public DbSet<CmsComponentDefinition> CmsComponentDefinitions => Set<CmsComponentDefinition>();
    public DbSet<CmsPage> CmsPages => Set<CmsPage>();
    public DbSet<CmsPageTranslation> CmsPageTranslations => Set<CmsPageTranslation>();
    public DbSet<CmsPageVersion> CmsPageVersions => Set<CmsPageVersion>();
    public DbSet<CmsSection> CmsSections => Set<CmsSection>();
    public DbSet<CmsContentValue> CmsContentValues => Set<CmsContentValue>();
    public DbSet<CmsMenu> CmsMenus => Set<CmsMenu>();
    public DbSet<CmsMenuItem> CmsMenuItems => Set<CmsMenuItem>();
    public DbSet<CmsMediaAsset> CmsMediaAssets => Set<CmsMediaAsset>();
    public DbSet<CmsMediaVariant> CmsMediaVariants => Set<CmsMediaVariant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HomeServiceDbContext).Assembly);
    }
}
