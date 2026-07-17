using HomeService.Application.Abstractions;
using HomeService.Contracts.Admin;
using HomeService.Contracts.Branding;
using HomeService.Contracts.Companies;
using HomeService.Contracts.Monitoring;
using HomeService.Contracts.Notifications;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminQueryService(IAppDbContext db)
{
    public async Task<AuditLogListResponse> ListAuditLogsAsync(AdminAuditLogQuery queryOptions, CancellationToken cancellationToken)
    {
        var pageSize = AdminAuditLogQuery.NormalizePageSize(queryOptions.Take);
        var offset = AdminAuditLogQuery.NormalizeOffset(queryOptions.Skip);
        var query = db.AuditLogEntries.AsNoTracking();

        if (Enum.TryParse<AuditActorType>(queryOptions.ActorType, true, out var parsedActorType))
        {
            query = query.Where(entry => entry.ActorType == parsedActorType);
        }

        if (queryOptions.ActorId.HasValue)
        {
            query = query.Where(entry => entry.ActorId == queryOptions.ActorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(queryOptions.Action))
        {
            var normalizedAction = queryOptions.Action.Trim();
            query = query.Where(entry => entry.Action == normalizedAction);
        }

        if (!string.IsNullOrWhiteSpace(queryOptions.EntityType))
        {
            var normalizedEntityType = queryOptions.EntityType.Trim();
            query = query.Where(entry => entry.EntityType == normalizedEntityType);
        }

        if (queryOptions.EntityId.HasValue)
        {
            query = query.Where(entry => entry.EntityId == queryOptions.EntityId.Value);
        }

        if (queryOptions.From.HasValue)
        {
            query = query.Where(entry => entry.OccurredAt >= queryOptions.From.Value);
        }

        if (queryOptions.To.HasValue)
        {
            query = query.Where(entry => entry.OccurredAt <= queryOptions.To.Value);
        }

        if (!string.IsNullOrWhiteSpace(queryOptions.Search))
        {
            var term = queryOptions.Search.Trim().ToLowerInvariant();
            query = query.Where(entry =>
                entry.Action.ToLower().Contains(term)
                || entry.EntityType.ToLower().Contains(term)
                || (entry.ActorDisplayName != null && entry.ActorDisplayName.ToLower().Contains(term))
                || (entry.Summary != null && entry.Summary.ToLower().Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(entry => entry.OccurredAt)
            .Skip(offset)
            .Take(pageSize)
            .Select(entry => new AuditLogEntryResponse(
                entry.Id,
                entry.ActorType.ToString(),
                entry.ActorId,
                entry.ActorDisplayName,
                entry.Action,
                entry.EntityType,
                entry.EntityId,
                entry.Summary,
                entry.BeforeJson,
                entry.AfterJson,
                entry.IpAddress,
                entry.UserAgent,
                entry.CorrelationId,
                entry.OccurredAt))
            .ToListAsync(cancellationToken);

        return new AuditLogListResponse(total, offset, pageSize, items);
    }

    public async Task<IReadOnlyList<CompanyApplicationSummaryResponse>> ListCompanyApplicationsAsync(CancellationToken cancellationToken)
    {
        var applications = await db.CompanyApplications
            .AsNoTracking()
            .OrderBy(application => application.Status == CompanyApplicationStatus.Approved
                || application.Status == CompanyApplicationStatus.ActivationSent
                || application.Status == CompanyApplicationStatus.Activated)
            .ThenByDescending(application => application.SubmittedAt)
            .Select(application => new
            {
                application.Id,
                application.CompanyName,
                application.City,
                application.ContactName,
                application.Email,
                application.PhoneNumber,
                Status = application.Status.ToString(),
                application.SubmittedAt,
                application.LastReminderSentAt,
                application.ActivationEmailSentAt
            })
            .ToListAsync(cancellationToken);

        var applicationIds = applications.Select(application => application.Id).ToList();
        var documents = await db.CompanyApplicationDocuments
            .AsNoTracking()
            .Where(document => applicationIds.Contains(document.CompanyApplicationId))
            .OrderBy(document => document.DocumentType)
            .Select(document => new
            {
                document.CompanyApplicationId,
                document.Id,
                DocumentType = document.DocumentType.ToString(),
                ReviewStatus = document.ReviewStatus.ToString(),
                document.ReviewNote
            })
            .ToListAsync(cancellationToken);

        var documentsByApplication = documents
            .GroupBy(document => document.CompanyApplicationId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(document => new CompanyApplicationDocumentSummaryResponse(
                        document.Id,
                        document.DocumentType,
                        document.ReviewStatus,
                        document.ReviewNote))
                    .ToList());

        return applications
            .Select(application =>
            {
                documentsByApplication.TryGetValue(application.Id, out var applicationDocuments);
                applicationDocuments ??= [];

                return new CompanyApplicationSummaryResponse(
                    application.Id,
                    application.CompanyName,
                    application.City,
                    application.ContactName,
                    application.Email,
                    application.PhoneNumber,
                    application.Status,
                    application.SubmittedAt,
                    application.LastReminderSentAt,
                    application.ActivationEmailSentAt,
                    applicationDocuments.Count,
                    applicationDocuments.Count(document => document.ReviewStatus == DocumentReviewStatus.Pending.ToString()),
                    applicationDocuments);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<NotificationOutboxMessageResponse>> ListNotificationsAsync(CancellationToken cancellationToken)
    {
        return await db.NotificationOutboxMessages
            .AsNoTracking()
            .OrderBy(notification => notification.Status)
            .ThenByDescending(notification => notification.ScheduledAt)
            .Take(100)
            .Select(notification => new NotificationOutboxMessageResponse(
                notification.Id,
                notification.Channel.ToString(),
                notification.Status.ToString(),
                notification.Recipient,
                notification.Subject,
                notification.Body,
                notification.RelatedEntityType,
                notification.RelatedEntityId,
                notification.ScheduledAt,
                notification.SentAt,
                notification.FailureReason))
            .ToListAsync(cancellationToken);
    }

    public async Task<AdminAccessSnapshotResponse> GetAccessSnapshotAsync(CancellationToken cancellationToken)
    {
        var modules = await db.AdminModules
            .AsNoTracking()
            .OrderBy(module => module.DisplayOrder)
            .Select(module => new AdminModuleSummaryResponse(
                module.Id,
                module.Key.ToString(),
                module.Name,
                module.Description,
                module.DisplayOrder,
                module.IsActive))
            .ToListAsync(cancellationToken);

        var roles = await db.AdminRoles
            .AsNoTracking()
            .OrderByDescending(role => role.IsSystemRole)
            .ThenBy(role => role.Name)
            .Select(role => new AdminRoleSummaryResponse(
                role.Id,
                role.Name,
                role.Description,
                role.IsSystemRole,
                role.IsActive,
                role.Permissions
                    .OrderBy(permission => permission.Module!.DisplayOrder)
                    .ThenBy(permission => permission.Action)
                    .Select(permission => new AdminPermissionSummaryResponse(
                        permission.ModuleId,
                        permission.Module!.Name,
                        permission.Action.ToString()))
                    .ToList()))
            .ToListAsync(cancellationToken);

        var admins = await db.AdminUsers
            .AsNoTracking()
            .OrderByDescending(admin => admin.IsSuperAdmin)
            .ThenBy(admin => admin.FullName)
            .Select(admin => new AdminUserSummaryResponse(
                admin.Id,
                admin.FullName,
                admin.Email,
                admin.IsSuperAdmin,
                admin.IsActive,
                admin.LastLoginAt,
                admin.Roles
                    .OrderBy(userRole => userRole.Role!.Name)
                    .Select(userRole => userRole.Role!.Name)
                    .ToList()))
            .ToListAsync(cancellationToken);

        return new AdminAccessSnapshotResponse(roles, modules, admins);
    }

    public async Task<CountryBrandingResponse?> GetCountryBrandingAsync(string countryCode, CancellationToken cancellationToken)
    {
        var normalizedCountryCode = countryCode.Trim().ToUpperInvariant();
        return await db.CountryBrandings
            .AsNoTracking()
            .Where(branding => branding.Country!.IsoCode == normalizedCountryCode)
            .Select(branding => new CountryBrandingResponse(
                branding.Country!.IsoCode,
                branding.Country.Name,
                branding.BrandName,
                branding.PrimaryColor,
                branding.SecondaryColor,
                branding.AccentColor,
                branding.HeroTitle,
                branding.HeroSubtitle,
                branding.HeroImageUrl,
                branding.MotifStyle))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CompanyApplicationDetailResponse?> GetCompanyApplicationAsync(Guid id, CancellationToken cancellationToken)
    {
        return await db.CompanyApplications
            .AsNoTracking()
            .Where(application => application.Id == id)
            .Select(application => new CompanyApplicationDetailResponse(
                application.Id,
                application.CompanyId,
                application.CompanyName,
                application.RegistrationNumber,
                application.City,
                application.Address,
                application.ContactName,
                application.Email,
                application.PhoneNumber,
                application.PlannedServices,
                application.EstimatedProviderCount,
                application.Status.ToString(),
                application.SubmittedAt,
                application.ReviewedAt,
                application.LastReminderSentAt,
                application.ActivationEmailSentAt,
                application.ActivatedAt,
                application.Company == null ? null : application.Company.AssignmentMode.ToString(),
                application.ActivationTokens
                    .OrderByDescending(token => token.CreatedAt)
                    .Select(token => token.ActivationLink)
                    .FirstOrDefault(),
                application.ActivationTokens
                    .OrderByDescending(token => token.CreatedAt)
                    .Select(token => (DateTimeOffset?)token.ExpiresAt)
                    .FirstOrDefault(),
                application.ReviewNote,
                application.Documents
                    .OrderBy(document => document.DocumentType)
                    .Select(document => new CompanyApplicationDocumentResponse(
                        document.Id,
                        document.DocumentType.ToString(),
                        document.OriginalFileName,
                        document.ContentType,
                        document.ReviewStatus.ToString(),
                        document.ReviewNote,
                        document.CreatedAt))
                    .ToList(),
                application.StatusHistory
                    .OrderBy(history => history.ChangedAt)
                    .Select(history => new CompanyApplicationStatusHistoryResponse(
                        history.Id,
                        history.PreviousStatus == null ? null : history.PreviousStatus.ToString(),
                        history.NewStatus.ToString(),
                        history.Note,
                        history.ChangedBy,
                        history.ChangedAt))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<AdminCompanyApplicationDocumentFile?> GetCompanyApplicationDocumentFileAsync(Guid id, CancellationToken cancellationToken)
    {
        return await db.CompanyApplicationDocuments
            .AsNoTracking()
            .Where(document => document.Id == id)
            .Select(document => new AdminCompanyApplicationDocumentFile(
                document.OriginalFileName,
                document.StoragePath,
                document.ContentType))
            .FirstOrDefaultAsync(cancellationToken);
    }
}

public sealed record AdminCompanyApplicationDocumentFile(
    string OriginalFileName,
    string StoragePath,
    string ContentType);

public sealed record AdminAuditLogQuery(
    string? ActorType,
    Guid? ActorId,
    string? Action,
    string? EntityType,
    Guid? EntityId,
    string? Search,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int? Skip,
    int? Take)
{
    public static int NormalizePageSize(int? take) => Math.Clamp(take ?? 50, 1, 200);
    public static int NormalizeOffset(int? skip) => Math.Max(skip ?? 0, 0);
}
