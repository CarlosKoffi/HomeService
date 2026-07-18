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
    public async Task<AdminCompanyListResponse> ListCompaniesAsync(
        string? status,
        string? search,
        CancellationToken cancellationToken)
    {
        var companiesQuery = db.Companies.AsNoTracking();

        if (Enum.TryParse<CompanyStatus>(status, true, out var parsedStatus))
        {
            companiesQuery = companiesQuery.Where(company => company.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            companiesQuery = companiesQuery.Where(company =>
                company.Name.ToLower().Contains(term)
                || company.PhoneNumber.ToLower().Contains(term)
                || (company.Email != null && company.Email.ToLower().Contains(term))
                || (company.City != null && company.City.ToLower().Contains(term)));
        }

        var companies = await companiesQuery
            .OrderByDescending(company => company.CreatedAt)
            .Take(200)
            .Select(company => new
            {
                company.Id,
                company.Name,
                company.Email,
                company.PhoneNumber,
                company.City,
                Status = company.Status.ToString(),
                AssignmentMode = company.AssignmentMode.ToString(),
                company.CreatedAt,
                ProviderCount = db.Providers.Count(provider => provider.CompanyId == company.Id),
                ActiveProviderCount = db.Providers.Count(provider => provider.CompanyId == company.Id && provider.Status == ProviderStatus.Approved),
                MissionCount = db.Missions.Count(mission => mission.CompanyId == company.Id),
                OpenMissionCount = db.Missions.Count(mission => mission.CompanyId == company.Id
                    && mission.Status != MissionStatus.Completed
                    && mission.Status != MissionStatus.Cancelled),
                DocumentCount = db.ProviderDocuments.Count(document => document.Provider!.CompanyId == company.Id)
            })
            .ToListAsync(cancellationToken);

        var stats = new AdminCompanyStatsResponse(
            await db.Companies.CountAsync(cancellationToken),
            await db.Companies.CountAsync(company => company.Status == CompanyStatus.Approved, cancellationToken),
            await db.Companies.CountAsync(company => company.Status == CompanyStatus.Suspended, cancellationToken),
            await db.Providers.CountAsync(cancellationToken),
            await db.Providers.CountAsync(provider => provider.Status == ProviderStatus.Approved, cancellationToken),
            await db.Missions.CountAsync(mission => mission.Status != MissionStatus.Completed && mission.Status != MissionStatus.Cancelled, cancellationToken),
            await db.Missions.CountAsync(mission => mission.Status == MissionStatus.Disputed, cancellationToken));

        return new AdminCompanyListResponse(
            companies
                .Select(company => new AdminCompanySummaryResponse(
                    company.Id,
                    company.Name,
                    company.Email,
                    company.PhoneNumber,
                    company.City,
                    company.Status,
                    company.AssignmentMode,
                    company.ProviderCount,
                    company.ActiveProviderCount,
                    company.MissionCount,
                    company.OpenMissionCount,
                    company.DocumentCount,
                    company.CreatedAt))
                .ToList(),
            stats);
    }

    public async Task<AdminCompanyDetailResponse?> GetCompanyAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var company = await db.Companies
            .AsNoTracking()
            .Where(company => company.Id == companyId)
            .Select(company => new
            {
                company.Id,
                company.Name,
                company.Email,
                company.PhoneNumber,
                company.LegalForm,
                company.RegistrationNumber,
                company.TaxIdentificationNumber,
                company.City,
                company.Address,
                company.InterventionZones,
                company.PlannedServices,
                company.WavePaymentNumber,
                company.OrangeMoneyPaymentNumber,
                Status = company.Status.ToString(),
                AssignmentMode = company.AssignmentMode.ToString(),
                company.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (company is null)
        {
            return null;
        }

        var providers = await db.Providers
            .AsNoTracking()
            .Where(provider => provider.CompanyId == companyId)
            .OrderBy(provider => provider.LastName)
            .ThenBy(provider => provider.FirstName)
            .Select(provider => new AdminCompanyProviderResponse(
                provider.Id,
                (provider.FirstName + " " + provider.LastName).Trim(),
                provider.PhoneNumber,
                provider.Email,
                provider.Gender.ToString(),
                provider.EmploymentType.ToString(),
                provider.Status.ToString(),
                provider.IsAvailable,
                provider.YearsOfExperience,
                provider.Address,
                provider.Services
                    .Where(service => service.IsActive)
                    .OrderBy(service => service.Service!.Name)
                    .Select(service => service.Service!.Name)
                    .ToList(),
                provider.Services
                    .Where(service => service.IsActive)
                    .SelectMany(service => service.Prestations)
                    .Where(prestation => prestation.IsActive)
                    .OrderBy(prestation => prestation.ServicePrestation!.Name)
                    .Select(prestation => prestation.ServicePrestation!.Name)
                    .ToList(),
                provider.Documents.Count,
                provider.CreatedAt))
            .ToListAsync(cancellationToken);

        var missions = await (
            from mission in db.Missions.AsNoTracking()
            join service in db.Services.AsNoTracking() on mission.ServiceId equals service.Id
            join customer in db.Customers.AsNoTracking() on mission.CustomerId equals customer.Id
            join provider in db.Providers.AsNoTracking() on mission.ProviderId equals provider.Id into providerJoin
            from provider in providerJoin.DefaultIfEmpty()
            where mission.CompanyId == companyId
            orderby mission.ScheduledFor ?? mission.CreatedAt descending
            select new AdminCompanyMissionResponse(
                mission.Id,
                service.Name,
                null,
                (customer.FirstName + " " + customer.LastName).Trim(),
                customer.PhoneNumber,
                provider == null ? null : provider.FirstName + " " + provider.LastName,
                mission.Status.ToString(),
                mission.PaymentStatus.ToString(),
                mission.PaymentMethod.ToString(),
                mission.ScheduledFor,
                mission.EstimatedTotalAmount,
                mission.CompanyQuotedAmount,
                mission.Currency,
                mission.ServiceAddress,
                mission.CreatedAt))
            .Take(100)
            .ToListAsync(cancellationToken);

        var documents = await db.ProviderDocuments
            .AsNoTracking()
            .Where(document => document.Provider != null && document.Provider.CompanyId == companyId)
            .OrderBy(document => document.Provider!.LastName)
            .ThenBy(document => document.DocumentType)
            .Select(document => new AdminCompanyDocumentResponse(
                document.Id,
                document.ProviderId,
                (document.Provider!.FirstName + " " + document.Provider.LastName).Trim(),
                document.DocumentType.ToString(),
                document.OriginalFileName,
                document.ContentType,
                $"/api/admin/provider-documents/{document.Id}/preview",
                document.CreatedAt))
            .ToListAsync(cancellationToken);

        var timeline = await db.CompanyApplicationStatusHistories
            .AsNoTracking()
            .Where(history => history.CompanyApplication!.CompanyId == companyId)
            .OrderByDescending(history => history.ChangedAt)
            .Select(history => new AdminCompanyApplicationTimelineResponse(
                history.Id,
                history.PreviousStatus == null ? null : history.PreviousStatus.ToString(),
                history.NewStatus.ToString(),
                history.Note,
                history.ChangedBy,
                history.ChangedAt))
            .Take(50)
            .ToListAsync(cancellationToken);

        return new AdminCompanyDetailResponse(
            company.Id,
            company.Name,
            company.Email,
            company.PhoneNumber,
            company.LegalForm,
            company.RegistrationNumber,
            company.TaxIdentificationNumber,
            company.City,
            company.Address,
            company.InterventionZones,
            company.PlannedServices,
            company.WavePaymentNumber,
            company.OrangeMoneyPaymentNumber,
            company.Status,
            company.AssignmentMode,
            company.CreatedAt,
            providers,
            missions,
            documents,
            timeline);
    }

    public async Task<AdminMissionListResponse> ListMissionsAsync(
        string? status,
        string? search,
        CancellationToken cancellationToken)
    {
        var query =
            from mission in db.Missions.AsNoTracking()
            join service in db.Services.AsNoTracking() on mission.ServiceId equals service.Id
            join customer in db.Customers.AsNoTracking() on mission.CustomerId equals customer.Id
            join company in db.Companies.AsNoTracking() on mission.CompanyId equals company.Id into companyJoin
            from company in companyJoin.DefaultIfEmpty()
            join provider in db.Providers.AsNoTracking() on mission.ProviderId equals provider.Id into providerJoin
            from provider in providerJoin.DefaultIfEmpty()
            select new
            {
                mission.Id,
                ServiceName = service.Name,
                CompanyName = company == null ? null : company.Name,
                CustomerName = (customer.FirstName + " " + customer.LastName).Trim(),
                CustomerPhoneNumber = customer.PhoneNumber,
                ProviderName = provider == null ? null : (provider.FirstName + " " + provider.LastName).Trim(),
                mission.Status,
                mission.PaymentStatus,
                mission.PaymentMethod,
                mission.ScheduledFor,
                Amount = mission.CompanyQuotedAmount ?? mission.EstimatedTotalAmount ?? mission.FinalTotalAmount,
                mission.Currency,
                mission.ServiceAddress,
                mission.CreatedAt
            };

        if (Enum.TryParse<MissionStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(mission => mission.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(mission =>
                mission.ServiceName.ToLower().Contains(term)
                || mission.CustomerName.ToLower().Contains(term)
                || mission.CustomerPhoneNumber.ToLower().Contains(term)
                || (mission.CompanyName != null && mission.CompanyName.ToLower().Contains(term))
                || (mission.ProviderName != null && mission.ProviderName.ToLower().Contains(term))
                || (mission.ServiceAddress != null && mission.ServiceAddress.ToLower().Contains(term)));
        }

        var missions = await query
            .OrderByDescending(mission => mission.ScheduledFor ?? mission.CreatedAt)
            .Take(250)
            .Select(mission => new AdminMissionSummaryResponse(
                mission.Id,
                mission.ServiceName,
                mission.CompanyName,
                mission.CustomerName,
                mission.CustomerPhoneNumber,
                mission.ProviderName,
                mission.Status.ToString(),
                mission.PaymentStatus.ToString(),
                mission.PaymentMethod.ToString(),
                mission.ScheduledFor,
                mission.Amount,
                mission.Currency,
                mission.ServiceAddress,
                mission.CreatedAt))
            .ToListAsync(cancellationToken);

        var stats = new AdminMissionStatsResponse(
            await db.Missions.CountAsync(cancellationToken),
            await db.Missions.CountAsync(mission => mission.Status != MissionStatus.Completed && mission.Status != MissionStatus.Cancelled, cancellationToken),
            await db.Missions.CountAsync(mission => mission.ScheduledFor != null && mission.Status != MissionStatus.Completed && mission.Status != MissionStatus.Cancelled, cancellationToken),
            await db.Missions.CountAsync(mission => mission.Status == MissionStatus.Completed, cancellationToken),
            await db.Missions.CountAsync(mission => mission.Status == MissionStatus.Disputed, cancellationToken));

        return new AdminMissionListResponse(missions, stats);
    }

    public async Task<AdminProviderDocumentFile?> GetProviderDocumentFileAsync(Guid id, CancellationToken cancellationToken)
    {
        return await db.ProviderDocuments
            .AsNoTracking()
            .Where(document => document.Id == id)
            .Select(document => new AdminProviderDocumentFile(
                document.OriginalFileName,
                document.StoragePath,
                document.ContentType))
            .FirstOrDefaultAsync(cancellationToken);
    }

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

public sealed record AdminProviderDocumentFile(
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
