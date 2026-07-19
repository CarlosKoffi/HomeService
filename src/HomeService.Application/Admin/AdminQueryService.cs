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

        var interimRequests = await db.ProviderAffiliationRequests
            .AsNoTracking()
            .Where(request => request.CompanyId == companyId)
            .OrderByDescending(request => request.RequestedAt)
            .Select(request => new AdminCompanyInterimRequestResponse(
                request.Id,
                request.ProviderId,
                (request.Provider!.FirstName + " " + request.Provider.LastName).Trim(),
                request.Provider.PhoneNumber,
                request.Provider.Email,
                request.Provider.Gender.ToString(),
                request.Status.ToString(),
                request.Message,
                request.ReviewNote,
                request.Provider.Services
                    .Where(service => service.IsActive)
                    .OrderBy(service => service.Service!.Name)
                    .Select(service => service.Service!.Name)
                    .ToList(),
                request.Provider.Services
                    .Where(service => service.IsActive)
                    .SelectMany(service => service.Prestations)
                    .Where(prestation => prestation.IsActive)
                    .OrderBy(prestation => prestation.ServicePrestation!.Name)
                    .Select(prestation => prestation.ServicePrestation!.Name)
                    .ToList(),
                request.RequestedAt,
                request.ReviewedAt))
            .Take(100)
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

        var applicationDocuments = await db.CompanyApplicationDocuments
            .AsNoTracking()
            .Where(document => document.CompanyApplication != null && document.CompanyApplication.CompanyId == companyId)
            .OrderBy(document => document.DocumentType)
            .ThenByDescending(document => document.CreatedAt)
            .Select(document => new AdminCompanyApplicationDocumentResponse(
                document.Id,
                document.DocumentType.ToString(),
                document.OriginalFileName,
                document.ContentType,
                document.ReviewStatus.ToString(),
                document.ReviewNote,
                $"/api/admin/company-application-documents/{document.Id}/preview",
                document.CreatedAt))
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

        var notifications = await db.CompanyPortalNotifications
            .AsNoTracking()
            .Where(notification => notification.CompanyId == companyId)
            .OrderByDescending(notification => notification.OccurredAt)
            .Select(notification => new AdminCompanyNotificationResponse(
                notification.Id,
                notification.Type,
                notification.Title,
                notification.Message,
                notification.Tone,
                notification.IsRead,
                notification.ActionUrl,
                notification.OccurredAt))
            .Take(50)
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

        var summary = new AdminCompanyOperationsSummaryResponse(
            providers.Count,
            providers.Count(provider => provider.Status == ProviderStatus.Approved.ToString()),
            providers.Count(provider => provider.EmploymentType == ProviderEmploymentType.TemporaryWorker.ToString()),
            interimRequests.Count(request => request.Status == ProviderAffiliationRequestStatus.Pending.ToString()),
            missions.Count(mission => mission.Status is not "Completed" and not "Cancelled"),
            missions.Count(mission => mission.Status == MissionStatus.Completed.ToString()),
            missions.Count(mission => mission.Status == MissionStatus.Disputed.ToString()),
            applicationDocuments.Count,
            applicationDocuments.Count(document => document.ReviewStatus == DocumentReviewStatus.Approved.ToString()),
            documents.Count,
            notifications.Count(notification => !notification.IsRead),
            missions
                .Where(mission => mission.PaymentStatus != PaymentStatus.Paid.ToString())
                .Sum(mission => mission.CompanyQuotedAmount ?? mission.EstimatedTotalAmount ?? 0),
            missions.FirstOrDefault()?.Currency ?? "XOF");

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
            timeline)
        {
            Summary = summary,
            InterimRequests = interimRequests,
            ApplicationDocuments = applicationDocuments,
            Notifications = notifications
        };
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

    public async Task<AdminMissionDetailResponse?> GetMissionAsync(Guid missionId, CancellationToken cancellationToken)
    {
        var mission = await (
            from item in db.Missions.AsNoTracking()
            join service in db.Services.AsNoTracking() on item.ServiceId equals service.Id
            join customer in db.Customers.AsNoTracking() on item.CustomerId equals customer.Id
            join company in db.Companies.AsNoTracking() on item.CompanyId equals company.Id into companyJoin
            from company in companyJoin.DefaultIfEmpty()
            join provider in db.Providers.AsNoTracking() on item.ProviderId equals provider.Id into providerJoin
            from provider in providerJoin.DefaultIfEmpty()
            where item.Id == missionId
            select new
            {
                item.Id,
                ServiceName = service.Name,
                CompanyName = company == null ? null : company.Name,
                item.CompanyId,
                CustomerName = (customer.FirstName + " " + customer.LastName).Trim(),
                CustomerPhoneNumber = customer.PhoneNumber,
                ProviderName = provider == null ? null : (provider.FirstName + " " + provider.LastName).Trim(),
                item.ProviderId,
                item.Status,
                item.Mode,
                item.PaymentStatus,
                item.PaymentMethod,
                item.ScheduledFor,
                item.EstimatedDurationMinutes,
                item.ActualDurationMinutes,
                item.EstimatedTotalAmount,
                item.FinalTotalAmount,
                item.CompanyQuotedAmount,
                item.CompanyQuoteJustification,
                item.CompanyQuotedAt,
                item.CustomerQuoteAcceptedAt,
                item.PlatformCommissionAmount,
                item.TransportFeeAmount,
                item.CancellationFeeAmount,
                item.Currency,
                item.ServiceAddress,
                item.ServiceLatitude,
                item.ServiceLongitude,
                item.ArrivalToleranceMeters,
                item.ProviderAcceptedAt,
                item.CustomerConfirmedAt,
                item.ContactDetailsReleasedAt,
                item.CanRevealContactDetails,
                item.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (mission is null)
        {
            return null;
        }

        var assignments = await db.ProviderMissionAssignments
            .AsNoTracking()
            .Where(assignment => assignment.MissionId == missionId)
            .OrderByDescending(assignment => assignment.CreatedAt)
            .Select(assignment => new AdminMissionAssignmentResponse(
                assignment.Id,
                assignment.ProviderId,
                (assignment.Provider!.FirstName + " " + assignment.Provider.LastName).Trim(),
                assignment.CompanyId,
                assignment.Company!.Name,
                assignment.Status.ToString(),
                assignment.ExpiresAt,
                assignment.RespondedAt,
                assignment.StartedAt,
                assignment.CompletedAt,
                assignment.RefusalReason == null ? null : assignment.RefusalReason.ToString(),
                assignment.RefusalComment,
                assignment.CompletionNote,
                assignment.ArrivalDistanceMeters,
                assignment.ArrivalToleranceMeters,
                assignment.ArrivalVerificationStatus.ToString(),
                assignment.ArrivalVerifiedAt))
            .ToListAsync(cancellationToken);

        var messages = await db.MissionMessages
            .AsNoTracking()
            .Where(message => message.Conversation != null && message.Conversation.MissionId == missionId)
            .OrderByDescending(message => message.CreatedAt)
            .Select(message => new AdminMissionConversationMessageResponse(
                message.Id,
                message.SenderType.ToString(),
                message.SenderId,
                message.Body,
                message.AttachmentContentType,
                message.ReadAt,
                message.CreatedAt))
            .Take(100)
            .ToListAsync(cancellationToken);

        return new AdminMissionDetailResponse(
            mission.Id,
            mission.ServiceName,
            mission.CompanyName,
            mission.CompanyId,
            mission.CustomerName,
            mission.CustomerPhoneNumber,
            mission.ProviderName,
            mission.ProviderId,
            mission.Status.ToString(),
            mission.Mode.ToString(),
            mission.PaymentStatus.ToString(),
            mission.PaymentMethod.ToString(),
            mission.ScheduledFor,
            mission.EstimatedDurationMinutes,
            mission.ActualDurationMinutes,
            mission.EstimatedTotalAmount,
            mission.FinalTotalAmount,
            mission.CompanyQuotedAmount,
            mission.CompanyQuoteJustification,
            mission.CompanyQuotedAt,
            mission.CustomerQuoteAcceptedAt,
            mission.PlatformCommissionAmount,
            mission.TransportFeeAmount,
            mission.CancellationFeeAmount,
            mission.Currency,
            mission.ServiceAddress,
            mission.ServiceLatitude,
            mission.ServiceLongitude,
            mission.ArrivalToleranceMeters,
            mission.ProviderAcceptedAt,
            mission.CustomerConfirmedAt,
            mission.ContactDetailsReleasedAt,
            mission.CanRevealContactDetails,
            mission.CreatedAt,
            assignments,
            messages);
    }

    public async Task<AdminProviderListResponse> ListProvidersAsync(
        string? status,
        string? employmentType,
        string? search,
        CancellationToken cancellationToken)
    {
        var query = db.Providers.AsNoTracking();

        if (Enum.TryParse<ProviderStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(provider => provider.Status == parsedStatus);
        }

        if (Enum.TryParse<ProviderEmploymentType>(employmentType, true, out var parsedEmploymentType))
        {
            query = query.Where(provider => provider.EmploymentType == parsedEmploymentType);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(provider =>
                provider.FirstName.ToLower().Contains(term)
                || provider.LastName.ToLower().Contains(term)
                || provider.PhoneNumber.ToLower().Contains(term)
                || provider.Address.ToLower().Contains(term)
                || (provider.Email != null && provider.Email.ToLower().Contains(term))
                || (provider.Company != null && provider.Company.Name.ToLower().Contains(term))
                || provider.Services.Any(service => service.Service != null && service.Service.Name.ToLower().Contains(term)));
        }

        var providers = await query
            .OrderByDescending(provider => provider.CreatedAt)
            .Take(250)
            .Select(provider => new AdminProviderSummaryResponse(
                provider.Id,
                provider.CompanyId,
                provider.Company == null ? null : provider.Company.Name,
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
                provider.Documents
                    .OrderBy(document => document.DocumentType)
                    .Select(document => new AdminProviderDocumentSummaryResponse(
                        document.Id,
                        document.DocumentType.ToString(),
                        document.OriginalFileName,
                        document.ContentType,
                        $"/api/admin/provider-documents/{document.Id}/preview",
                        document.CreatedAt))
                    .ToList(),
                provider.CreatedAt))
            .ToListAsync(cancellationToken);

        var stats = new AdminProviderStatsResponse(
            await db.Providers.CountAsync(cancellationToken),
            await db.Providers.CountAsync(provider => provider.Status == ProviderStatus.Approved, cancellationToken),
            await db.Providers.CountAsync(provider => provider.Status == ProviderStatus.InterimCandidate, cancellationToken),
            await db.Providers.CountAsync(provider => provider.Status == ProviderStatus.SuspendedByCompany || provider.Status == ProviderStatus.SuspendedByPlatform, cancellationToken),
            await db.Providers.CountAsync(provider => provider.IsAvailable, cancellationToken));

        return new AdminProviderListResponse(providers, stats);
    }

    public async Task<AdminProviderDetailResponse?> GetProviderAsync(Guid providerId, CancellationToken cancellationToken)
    {
        var provider = await db.Providers
            .AsNoTracking()
            .Where(provider => provider.Id == providerId)
            .Select(provider => new
            {
                provider.Id,
                provider.CompanyId,
                CompanyName = provider.Company == null ? null : provider.Company.Name,
                provider.FirstName,
                provider.LastName,
                provider.PhoneNumber,
                provider.Email,
                provider.DateOfBirth,
                provider.Gender,
                provider.EmploymentType,
                provider.Status,
                provider.RegistrationSource,
                provider.IsAvailable,
                provider.YearsOfExperience,
                provider.Address,
                provider.MissionLatitude,
                provider.MissionLongitude,
                provider.MissionRadiusKm,
                provider.CurrentLatitude,
                provider.CurrentLongitude,
                provider.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (provider is null)
        {
            return null;
        }

        var services = await db.ProviderServices
            .AsNoTracking()
            .Where(service => service.ProviderId == providerId)
            .OrderBy(service => service.Service!.Name)
            .Select(service => new AdminProviderServiceDetailResponse(
                service.ServiceId,
                service.Service!.Name,
                service.ExperienceLevel.ToString(),
                service.YearsOfExperience,
                service.PriceTier.ToString(),
                service.IsActive,
                service.Prestations
                    .Where(prestation => prestation.IsActive)
                    .OrderBy(prestation => prestation.ServicePrestation!.Name)
                    .Select(prestation => prestation.ServicePrestation!.Name)
                    .ToList()))
            .ToListAsync(cancellationToken);

        var candidateServices = await db.ProviderCandidateServices
            .AsNoTracking()
            .Where(service => service.ProviderId == providerId)
            .OrderBy(service => service.Service!.Name)
            .Select(service => new AdminProviderServiceDetailResponse(
                service.ServiceId,
                service.Service!.Name,
                service.ExperienceLevel.ToString(),
                service.YearsOfExperience,
                "Normal",
                service.IsActive,
                Array.Empty<string>()))
            .ToListAsync(cancellationToken);

        var documents = await db.ProviderDocuments
            .AsNoTracking()
            .Where(document => document.ProviderId == providerId)
            .OrderBy(document => document.DocumentType)
            .Select(document => new AdminProviderDocumentSummaryResponse(
                document.Id,
                document.DocumentType.ToString(),
                document.OriginalFileName,
                document.ContentType,
                $"/api/admin/provider-documents/{document.Id}/preview",
                document.CreatedAt))
            .ToListAsync(cancellationToken);

        var affiliationRequests = await db.ProviderAffiliationRequests
            .AsNoTracking()
            .Where(request => request.ProviderId == providerId)
            .OrderByDescending(request => request.RequestedAt)
            .Select(request => new AdminProviderAffiliationRequestDetailResponse(
                request.Id,
                request.CompanyId,
                request.Company!.Name,
                request.Status.ToString(),
                request.Message,
                request.ReviewNote,
                request.RequestedAt,
                request.ReviewedAt))
            .ToListAsync(cancellationToken);

        var assignments = await (
            from assignment in db.ProviderMissionAssignments.AsNoTracking()
            join mission in db.Missions.AsNoTracking() on assignment.MissionId equals mission.Id
            join service in db.Services.AsNoTracking() on mission.ServiceId equals service.Id
            join company in db.Companies.AsNoTracking() on assignment.CompanyId equals company.Id
            where assignment.ProviderId == providerId
            orderby assignment.CreatedAt descending
            select new AdminProviderMissionAssignmentDetailResponse(
                assignment.Id,
                assignment.MissionId,
                service.Name,
                company.Name,
                assignment.Status.ToString(),
                assignment.ExpiresAt,
                assignment.RespondedAt,
                assignment.StartedAt,
                assignment.CompletedAt,
                assignment.RefusalReason == null ? null : assignment.RefusalReason.ToString(),
                assignment.RefusalComment,
                assignment.ArrivalVerificationStatus.ToString(),
                assignment.ArrivalDistanceMeters))
            .Take(100)
            .ToListAsync(cancellationToken);

        return new AdminProviderDetailResponse(
            provider.Id,
            provider.CompanyId,
            provider.CompanyName,
            $"{provider.FirstName} {provider.LastName}".Trim(),
            provider.FirstName,
            provider.LastName,
            provider.PhoneNumber,
            provider.Email,
            provider.DateOfBirth,
            provider.Gender.ToString(),
            provider.EmploymentType.ToString(),
            provider.Status.ToString(),
            provider.RegistrationSource.ToString(),
            provider.IsAvailable,
            provider.YearsOfExperience,
            provider.Address,
            provider.MissionLatitude,
            provider.MissionLongitude,
            provider.MissionRadiusKm,
            provider.CurrentLatitude,
            provider.CurrentLongitude,
            provider.CreatedAt,
            services,
            candidateServices,
            documents,
            affiliationRequests,
            assignments);
    }

    public async Task<AdminPaymentListResponse> ListPaymentsAsync(
        string? period,
        string? paymentStatus,
        string? paymentMethod,
        string? search,
        CancellationToken cancellationToken)
    {
        var periodStart = GetPaymentPeriodStart(period);
        var query =
            from mission in db.Missions.AsNoTracking()
            join service in db.Services.AsNoTracking() on mission.ServiceId equals service.Id
            join customer in db.Customers.AsNoTracking() on mission.CustomerId equals customer.Id
            join company in db.Companies.AsNoTracking() on mission.CompanyId equals company.Id into companyJoin
            from company in companyJoin.DefaultIfEmpty()
            join provider in db.Providers.AsNoTracking() on mission.ProviderId equals provider.Id into providerJoin
            from provider in providerJoin.DefaultIfEmpty()
            where mission.CreatedAt >= periodStart || (mission.ScheduledFor != null && mission.ScheduledFor >= periodStart)
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
                Amount = mission.CompanyQuotedAmount ?? mission.FinalTotalAmount ?? mission.EstimatedTotalAmount ?? 0,
                mission.PlatformCommissionAmount,
                mission.TransportFeeAmount,
                mission.CancellationFeeAmount,
                mission.Currency,
                mission.ScheduledFor,
                mission.CreatedAt
            };

        if (Enum.TryParse<PaymentStatus>(paymentStatus, true, out var parsedPaymentStatus))
        {
            query = query.Where(payment => payment.PaymentStatus == parsedPaymentStatus);
        }

        if (Enum.TryParse<PaymentMethod>(paymentMethod, true, out var parsedPaymentMethod))
        {
            query = query.Where(payment => payment.PaymentMethod == parsedPaymentMethod);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(payment =>
                payment.ServiceName.ToLower().Contains(term)
                || payment.CustomerName.ToLower().Contains(term)
                || payment.CustomerPhoneNumber.ToLower().Contains(term)
                || (payment.CompanyName != null && payment.CompanyName.ToLower().Contains(term))
                || (payment.ProviderName != null && payment.ProviderName.ToLower().Contains(term)));
        }

        var items = await query
            .OrderByDescending(payment => payment.ScheduledFor ?? payment.CreatedAt)
            .Take(300)
            .Select(payment => new AdminPaymentMissionResponse(
                payment.Id,
                payment.ServiceName,
                payment.CompanyName,
                payment.CustomerName,
                payment.CustomerPhoneNumber,
                payment.ProviderName,
                payment.Status.ToString(),
                payment.PaymentStatus.ToString(),
                payment.PaymentMethod.ToString(),
                payment.Amount,
                payment.PlatformCommissionAmount,
                payment.TransportFeeAmount,
                payment.CancellationFeeAmount,
                payment.Currency,
                payment.ScheduledFor,
                payment.CreatedAt))
            .ToListAsync(cancellationToken);

        var paidItems = items.Where(item => item.PaymentStatus == PaymentStatus.Paid.ToString()).ToList();
        var pendingItems = items.Where(item => item.PaymentStatus is nameof(PaymentStatus.Pending) or nameof(PaymentStatus.Authorized)).ToList();
        var disputedItems = items.Where(item => item.PaymentStatus is nameof(PaymentStatus.Failed) or nameof(PaymentStatus.Refunded)).ToList();
        var stats = new AdminPaymentStatsResponse(
            items.Sum(item => item.Amount),
            paidItems.Sum(item => item.Amount),
            pendingItems.Sum(item => item.Amount),
            pendingItems.Where(item => item.PaymentMethod == PaymentMethod.Cash.ToString()).Sum(item => item.Amount),
            paidItems.Where(item => item.PaymentMethod == PaymentMethod.MobileMoney.ToString()).Sum(item => item.Amount),
            items.Sum(item => item.PlatformCommissionAmount),
            disputedItems.Sum(item => item.Amount),
            items.Count);

        return new AdminPaymentListResponse(items, stats);
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

    private static DateTimeOffset GetPaymentPeriodStart(string? period)
    {
        var now = DateTimeOffset.UtcNow;
        return period?.Trim().ToLowerInvariant() switch
        {
            "week" => now.AddDays(-7),
            "year" => new DateTimeOffset(now.Year, 1, 1, 0, 0, 0, TimeSpan.Zero),
            _ => new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero)
        };
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

        if (queryOptions.ContextId.HasValue && !string.IsNullOrWhiteSpace(queryOptions.ContextType))
        {
            query = await ApplyAuditContextAsync(
                query,
                queryOptions.ContextType,
                queryOptions.ContextId.Value,
                cancellationToken);
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

    private async Task<IQueryable<Domain.Entities.AuditLogEntry>> ApplyAuditContextAsync(
        IQueryable<Domain.Entities.AuditLogEntry> query,
        string contextType,
        Guid contextId,
        CancellationToken cancellationToken)
    {
        return contextType.Trim().ToLowerInvariant() switch
        {
            "company" => await ApplyCompanyAuditContextAsync(query, contextId, cancellationToken),
            "provider" => await ApplyProviderAuditContextAsync(query, contextId, cancellationToken),
            "mission" => await ApplyMissionAuditContextAsync(query, contextId, cancellationToken),
            _ => query
        };
    }

    private async Task<IQueryable<Domain.Entities.AuditLogEntry>> ApplyCompanyAuditContextAsync(
        IQueryable<Domain.Entities.AuditLogEntry> query,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var providerIds = await db.Providers
            .AsNoTracking()
            .Where(provider => provider.CompanyId == companyId)
            .Select(provider => provider.Id)
            .ToListAsync(cancellationToken);

        var missionIds = await db.Missions
            .AsNoTracking()
            .Where(mission => mission.CompanyId == companyId)
            .Select(mission => mission.Id)
            .ToListAsync(cancellationToken);

        var applicationIds = await db.CompanyApplications
            .AsNoTracking()
            .Where(application => application.CompanyId == companyId)
            .Select(application => application.Id)
            .ToListAsync(cancellationToken);

        return query.Where(entry =>
            (entry.ActorType == AuditActorType.Company && entry.ActorId == companyId)
            || (entry.EntityType == "Company" && entry.EntityId == companyId)
            || (entry.EntityType == "CompanyApplication" && entry.EntityId != null && applicationIds.Contains(entry.EntityId.Value))
            || (entry.EntityType == "ProviderProfile" && entry.EntityId != null && providerIds.Contains(entry.EntityId.Value))
            || (entry.EntityType == "Mission" && entry.EntityId != null && missionIds.Contains(entry.EntityId.Value)));
    }

    private async Task<IQueryable<Domain.Entities.AuditLogEntry>> ApplyProviderAuditContextAsync(
        IQueryable<Domain.Entities.AuditLogEntry> query,
        Guid providerId,
        CancellationToken cancellationToken)
    {
        var assignmentIds = await db.ProviderMissionAssignments
            .AsNoTracking()
            .Where(assignment => assignment.ProviderId == providerId)
            .Select(assignment => assignment.Id)
            .ToListAsync(cancellationToken);

        var affiliationRequestIds = await db.ProviderAffiliationRequests
            .AsNoTracking()
            .Where(request => request.ProviderId == providerId)
            .Select(request => request.Id)
            .ToListAsync(cancellationToken);

        return query.Where(entry =>
            (entry.ActorType == AuditActorType.Provider && entry.ActorId == providerId)
            || (entry.EntityType == "ProviderProfile" && entry.EntityId == providerId)
            || (entry.EntityType == "ProviderMissionAssignment" && entry.EntityId != null && assignmentIds.Contains(entry.EntityId.Value))
            || (entry.EntityType == "ProviderAffiliationRequest" && entry.EntityId != null && affiliationRequestIds.Contains(entry.EntityId.Value)));
    }

    private async Task<IQueryable<Domain.Entities.AuditLogEntry>> ApplyMissionAuditContextAsync(
        IQueryable<Domain.Entities.AuditLogEntry> query,
        Guid missionId,
        CancellationToken cancellationToken)
    {
        var assignmentIds = await db.ProviderMissionAssignments
            .AsNoTracking()
            .Where(assignment => assignment.MissionId == missionId)
            .Select(assignment => assignment.Id)
            .ToListAsync(cancellationToken);

        var conversationIds = await db.MissionConversations
            .AsNoTracking()
            .Where(conversation => conversation.MissionId == missionId)
            .Select(conversation => conversation.Id)
            .ToListAsync(cancellationToken);

        return query.Where(entry =>
            (entry.EntityType == "Mission" && entry.EntityId == missionId)
            || (entry.EntityType == "ProviderMissionAssignment" && entry.EntityId != null && assignmentIds.Contains(entry.EntityId.Value))
            || (entry.EntityType == "MissionConversation" && entry.EntityId != null && conversationIds.Contains(entry.EntityId.Value)));
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
    int? Take,
    string? ContextType = null,
    Guid? ContextId = null)
{
    public static int NormalizePageSize(int? take) => Math.Clamp(take ?? 50, 1, 200);
    public static int NormalizeOffset(int? skip) => Math.Max(skip ?? 0, 0);
}
