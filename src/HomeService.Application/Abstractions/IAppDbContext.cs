using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<Company> Companies { get; }
    DbSet<CompanyPortalUser> CompanyPortalUsers { get; }
    DbSet<CompanyPortalSession> CompanyPortalSessions { get; }
    DbSet<CompanyApplication> CompanyApplications { get; }
    DbSet<CompanyApplicationDocument> CompanyApplicationDocuments { get; }
    DbSet<CompanyApplicationService> CompanyApplicationServices { get; }
    DbSet<CompanyApplicationStatusHistory> CompanyApplicationStatusHistories { get; }
    DbSet<CompanyActivationToken> CompanyActivationTokens { get; }
    DbSet<Service> Services { get; }
    DbSet<ProviderProfile> Providers { get; }
    DbSet<ProviderInvitation> ProviderInvitations { get; }
    DbSet<ProviderAffiliationRequest> ProviderAffiliationRequests { get; }
    DbSet<ProviderCandidateService> ProviderCandidateServices { get; }
    DbSet<ProviderPortalSession> ProviderPortalSessions { get; }
    DbSet<ProviderDocument> ProviderDocuments { get; }
    DbSet<ProviderService> ProviderServices { get; }
    DbSet<ProviderServicePortfolioItem> ProviderServicePortfolioItems { get; }
    DbSet<CustomerProfile> Customers { get; }
    DbSet<Mission> Missions { get; }
    DbSet<ProviderMissionAssignment> ProviderMissionAssignments { get; }
    DbSet<MissionConversation> MissionConversations { get; }
    DbSet<MissionMessage> MissionMessages { get; }
    DbSet<Country> Countries { get; }
    DbSet<CountryBranding> CountryBrandings { get; }
    DbSet<Language> Languages { get; }
    DbSet<TranslationKey> TranslationKeys { get; }
    DbSet<TranslationValue> TranslationValues { get; }
    DbSet<AdminUser> AdminUsers { get; }
    DbSet<AdminRole> AdminRoles { get; }
    DbSet<AdminModule> AdminModules { get; }
    DbSet<AdminRolePermission> AdminRolePermissions { get; }
    DbSet<AdminUserRole> AdminUserRoles { get; }
    DbSet<NotificationOutboxMessage> NotificationOutboxMessages { get; }
    DbSet<AuditLogEntry> AuditLogEntries { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
