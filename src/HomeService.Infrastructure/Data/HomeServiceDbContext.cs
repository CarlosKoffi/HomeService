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
    public DbSet<CompanyApplication> CompanyApplications => Set<CompanyApplication>();
    public DbSet<CompanyApplicationDocument> CompanyApplicationDocuments => Set<CompanyApplicationDocument>();
    public DbSet<CompanyApplicationService> CompanyApplicationServices => Set<CompanyApplicationService>();
    public DbSet<CompanyApplicationStatusHistory> CompanyApplicationStatusHistories => Set<CompanyApplicationStatusHistory>();
    public DbSet<CompanyActivationToken> CompanyActivationTokens => Set<CompanyActivationToken>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<ProviderProfile> Providers => Set<ProviderProfile>();
    public DbSet<ProviderInvitation> ProviderInvitations => Set<ProviderInvitation>();
    public DbSet<ProviderPortalSession> ProviderPortalSessions => Set<ProviderPortalSession>();
    public DbSet<ProviderDocument> ProviderDocuments => Set<ProviderDocument>();
    public DbSet<ProviderService> ProviderServices => Set<ProviderService>();
    public DbSet<ProviderServicePortfolioItem> ProviderServicePortfolioItems => Set<ProviderServicePortfolioItem>();
    public DbSet<CustomerProfile> Customers => Set<CustomerProfile>();
    public DbSet<Mission> Missions => Set<Mission>();
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
    public DbSet<NotificationOutboxMessage> NotificationOutboxMessages => Set<NotificationOutboxMessage>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HomeServiceDbContext).Assembly);
    }
}
