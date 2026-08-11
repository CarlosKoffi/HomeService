using HomeService.Application.Abstractions;
using HomeService.Application.Missions;
using HomeService.Application.Notifications;
using HomeService.Contracts.CompanyPortal;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.CompanyPortal;

public sealed class CompanyPortalQueryService(
    IAppDbContext db,
    MobileNavigationBadgeService? navigationBadges = null)
{
    public async Task<CompanyPortalProfileResult> GetProfileAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var company = await db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(company => company.Id == companyId && company.Status != CompanyStatus.Suspended, cancellationToken);
        if (company is null)
        {
            return CompanyPortalProfileResult.NotFound();
        }

        var application = await db.CompanyApplications
            .AsNoTracking()
            .Where(application => application.CompanyId == companyId)
            .OrderByDescending(application => application.CreatedAt)
            .Select(application => new CompanyPortalProfileResponse(
                company.Id,
                application.Id,
                application.CompanyName,
                application.RegistrationNumber ?? company.RegistrationNumber,
                application.LegalForm ?? company.LegalForm,
                application.TaxIdentificationNumber ?? company.TaxIdentificationNumber,
                application.City,
                application.Address ?? company.Address,
                application.ContactName,
                application.Email,
                application.PhoneNumber,
                application.PlannedServices ?? company.PlannedServices,
                application.InterventionZones ?? company.InterventionZones,
                application.WavePaymentNumber ?? company.WavePaymentNumber,
                application.OrangeMoneyPaymentNumber ?? company.OrangeMoneyPaymentNumber,
                application.MtnMoneyPaymentNumber ?? company.MtnMoneyPaymentNumber,
                application.MoovMoneyPaymentNumber ?? company.MoovMoneyPaymentNumber,
                application.EstimatedProviderCount,
                company.Status.ToString(),
                application.Status.ToString(),
                company.Status == CompanyStatus.Approved,
                application.ReviewNote,
                application.Documents
                    .OrderBy(document => document.DocumentType)
                    .ThenByDescending(document => document.CreatedAt)
                    .Select(document => new CompanyPortalProfileDocumentResponse(
                        document.Id,
                        document.DocumentType.ToString(),
                        GetCompanyDocumentLabel(document.DocumentType),
                        document.OriginalFileName,
                        document.ContentType,
                        document.ReviewStatus.ToString(),
                        document.ReviewNote,
                        document.CreatedAt,
                        $"/api/admin/company-application-documents/{document.Id}/download"))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        return CompanyPortalProfileResult.Ok(application ?? new CompanyPortalProfileResponse(
            company.Id,
            null,
            company.Name,
            company.RegistrationNumber,
            company.LegalForm,
            company.TaxIdentificationNumber,
            company.City ?? string.Empty,
            company.Address,
            string.Empty,
            company.Email ?? string.Empty,
            company.PhoneNumber,
            company.PlannedServices,
            company.InterventionZones,
            company.WavePaymentNumber,
            company.OrangeMoneyPaymentNumber,
            company.MtnMoneyPaymentNumber,
            company.MoovMoneyPaymentNumber,
            null,
            company.Status.ToString(),
            "Submitted",
            company.Status == CompanyStatus.Approved,
            null,
            []));
    }

    public async Task<CompanyPortalMissionsResult> ListMissionsAsync(Guid companyId, string? view, CancellationToken cancellationToken)
    {
        if (!await CompanyExistsAsync(companyId, cancellationToken))
        {
            return CompanyPortalMissionsResult.NotFound();
        }

        var now = DateTimeOffset.UtcNow;
        var query = from mission in db.Missions.AsNoTracking()
                    where mission.CompanyId == companyId
                    join service in db.Services.AsNoTracking() on mission.ServiceId equals service.Id
                    join customer in db.Customers.AsNoTracking() on mission.CustomerId equals customer.Id
                    join provider in db.Providers.AsNoTracking() on mission.ProviderId equals provider.Id into providerJoin
                    from provider in providerJoin.DefaultIfEmpty()
                    select new { mission, service, customer, provider };

        query = view?.Trim().ToLowerInvariant() switch
        {
            "upcoming" => query.Where(row => row.mission.ScheduledFor >= now && row.mission.Status != MissionStatus.Completed && row.mission.Status != MissionStatus.Cancelled),
            "past" => query.Where(row => row.mission.Status == MissionStatus.Completed || row.mission.Status == MissionStatus.Cancelled),
            "live" => query.Where(row => row.mission.Status == MissionStatus.SearchingProvider || row.mission.Status == MissionStatus.Offered || row.mission.Status == MissionStatus.Accepted || row.mission.Status == MissionStatus.OnTheWay || row.mission.Status == MissionStatus.Started),
            _ => query
        };

        var missions = await query
            .OrderBy(row => row.mission.ScheduledFor ?? row.mission.CreatedAt)
            .Select(row => new CompanyPortalMissionResponse(
                row.mission.Id,
                row.mission.MissionNumber,
                row.service.Name,
                row.customer.FirstName + " " + row.customer.LastName,
                row.customer.PhoneNumber,
                row.mission.Mode.ToString(),
                row.mission.Status.ToString(),
                row.mission.PaymentMethod.ToString(),
                row.mission.PaymentStatus.ToString(),
                row.mission.ScheduledFor,
                row.mission.EstimatedDurationMinutes,
                row.mission.FinalTotalAmount ?? row.mission.EstimatedTotalAmount,
                row.mission.Currency,
                row.mission.ProviderId,
                row.provider == null ? null : row.provider.FirstName + " " + row.provider.LastName,
                row.mission.CompanyQuotedAmount,
                row.mission.CompanyQuoteJustification,
                row.mission.CompanyQuotedAt,
                row.mission.CustomerQuoteAcceptedAt,
                row.service.IconName,
                row.mission.ServiceAddress,
                row.mission.ActualDurationMinutes,
                null,
                row.mission.Status == MissionStatus.Cancelled
                    ? row.mission.CancellationReason.ToString()
                    : null,
                row.mission.PlatformCommissionAmount,
                row.mission.CompanyAssignmentExpiresAt,
                row.mission.ServiceLatitude,
                row.mission.ServiceLongitude,
                row.mission.CancelledBy == null ? null : row.mission.CancelledBy.ToString(),
                row.mission.CancellationComment,
                0))
            .ToListAsync(cancellationToken);

        var unreadMessageCounts = await (navigationBadges ?? new MobileNavigationBadgeService(db))
            .GetUnreadMessageCountsByMissionAsync(
                MobileDeviceOwnerType.Company,
                companyId,
                cancellationToken);
        missions = missions
            .Select(mission => mission with
            {
                UnreadMessageCount = unreadMessageCounts.GetValueOrDefault(mission.Id)
            })
            .ToList();

        return CompanyPortalMissionsResult.Ok(missions.Select(HideClosedMissionCustomerContact).ToList());
    }

    public async Task<CompanyPortalMissionDetailResult> GetMissionDetailAsync(
        Guid companyId,
        Guid missionId,
        CancellationToken cancellationToken)
    {
        if (!await CompanyExistsAsync(companyId, cancellationToken))
        {
            return CompanyPortalMissionDetailResult.NotFound();
        }

        var now = DateTimeOffset.UtcNow;
        var row = await (
                from missionRow in db.Missions.AsNoTracking()
                where missionRow.Id == missionId
                    && (missionRow.CompanyId == companyId
                        || db.MissionDispatchOffers.Any(offer =>
                            offer.MissionId == missionRow.Id
                            && offer.CompanyId == companyId
                            && offer.Status == MissionDispatchOfferStatus.Sent
                            && offer.ExpiresAt > now))
                join service in db.Services.AsNoTracking() on missionRow.ServiceId equals service.Id
                join customer in db.Customers.AsNoTracking() on missionRow.CustomerId equals customer.Id
                join provider in db.Providers.AsNoTracking() on missionRow.ProviderId equals provider.Id into providerJoin
                from provider in providerJoin.DefaultIfEmpty()
                join prestation in db.ServicePrestations.AsNoTracking() on missionRow.ServicePrestationId equals prestation.Id into prestationJoin
                from prestation in prestationJoin.DefaultIfEmpty()
                join option in db.ServiceOptions.AsNoTracking() on missionRow.ServiceOptionId equals option.Id into optionJoin
                from option in optionJoin.DefaultIfEmpty()
                select new
                {
                    Mission = missionRow,
                    ServiceName = service.Name,
                    ServiceIconName = service.IconName,
                    CustomerId = customer.Id,
                    CustomerName = customer.FirstName + " " + customer.LastName,
                    customer.PhoneNumber,
                    PrestationName = prestation == null ? null : prestation.Name,
                    OptionName = option == null ? null : option.Name,
                    ProviderName = provider == null ? null : provider.FirstName + " " + provider.LastName,
                    ProviderPhoneNumber = provider == null ? null : provider.PhoneNumber,
                    ProviderPhotoUrl = provider == null
                        ? null
                        : provider.Documents
                            .Where(document => document.DocumentType == ProviderDocumentType.Photo)
                            .OrderByDescending(document => document.CreatedAt)
                            .Select(document => $"/api/company-portal/provider-documents/{document.Id}/preview")
                            .FirstOrDefault(),
                    ProviderLatitude = provider == null ? null : provider.CurrentLatitude ?? provider.MissionLatitude,
                    ProviderLongitude = provider == null ? null : provider.CurrentLongitude ?? provider.MissionLongitude
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return CompanyPortalMissionDetailResult.NotFound();
        }

        var offer = await db.MissionDispatchOffers
            .AsNoTracking()
            .Where(item => item.MissionId == missionId && item.CompanyId == companyId)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var offerOpen = offer?.Status == MissionDispatchOfferStatus.Sent && offer.ExpiresAt > now;
        var mission = row.Mission;
        var canAssign = mission.CompanyId == companyId
            && mission.ProviderId is null
            && mission.Status == MissionStatus.SearchingProvider;
        var canAccessCustomerContact = MissionCustomerContactAccessPolicy.CanAccess(mission.Status);

        var history = await (
                from historyMission in db.Missions.AsNoTracking()
                where historyMission.Id != missionId
                    && historyMission.CompanyId == companyId
                    && historyMission.CustomerId == row.CustomerId
                join service in db.Services.AsNoTracking() on historyMission.ServiceId equals service.Id
                join prestation in db.ServicePrestations.AsNoTracking() on historyMission.ServicePrestationId equals prestation.Id into prestationJoin
                from prestation in prestationJoin.DefaultIfEmpty()
                orderby historyMission.CreatedAt descending
                select new CompanyCustomerMissionHistoryResponse(
                    historyMission.Id,
                    historyMission.MissionNumber,
                    service.Name,
                    prestation == null ? null : prestation.Name,
                    historyMission.Status.ToString(),
                    historyMission.CustomerCompletionValidatedAt ?? historyMission.ScheduledFor ?? historyMission.UpdatedAt ?? historyMission.CreatedAt,
                    db.MissionReviews
                        .Where(review => review.MissionId == historyMission.Id)
                        .Select(review => (int?)review.OverallRating)
                        .FirstOrDefault()))
            .Take(20)
            .ToListAsync(cancellationToken);

        var response = new CompanyPortalMissionResponse(
            mission.Id,
            mission.MissionNumber,
            row.ServiceName,
            row.CustomerName,
            canAccessCustomerContact ? row.PhoneNumber : string.Empty,
            mission.Mode.ToString(),
            mission.Status.ToString(),
            mission.PaymentMethod.ToString(),
            mission.PaymentStatus.ToString(),
            mission.ScheduledFor,
            mission.EstimatedDurationMinutes,
            mission.FinalTotalAmount ?? mission.EstimatedTotalAmount,
            mission.Currency,
            mission.ProviderId,
            row.ProviderName,
            mission.CompanyQuotedAmount,
            mission.CompanyQuoteJustification,
            mission.CompanyQuotedAt,
            mission.CustomerQuoteAcceptedAt,
            row.ServiceIconName,
            canAccessCustomerContact ? mission.ServiceAddress : null,
            mission.ActualDurationMinutes,
            null,
            mission.Status == MissionStatus.Cancelled
                ? mission.CancellationReason.ToString()
                : null,
            mission.PlatformCommissionAmount,
            mission.CompanyAssignmentExpiresAt,
            canAccessCustomerContact ? mission.ServiceLatitude : null,
            canAccessCustomerContact ? mission.ServiceLongitude : null,
            mission.CancelledBy?.ToString(),
            mission.CancellationComment);

        return CompanyPortalMissionDetailResult.Ok(new CompanyPortalMissionDetailResponse(
            response,
            row.PrestationName,
            row.OptionName,
            mission.Description,
            mission.CreatedAt,
            offer?.Id,
            offerOpen ? "Pending" : offer?.Status.ToString(),
            offer?.ExpiresAt,
            offerOpen,
            offerOpen,
            canAssign,
            row.ProviderPhoneNumber,
            row.ProviderLatitude,
            row.ProviderLongitude,
            canAccessCustomerContact
                ? CalculateDistanceKilometers(
                    mission.ServiceLatitude,
                    mission.ServiceLongitude,
                    row.ProviderLatitude,
                    row.ProviderLongitude)
                : null,
            history,
            row.ProviderPhotoUrl));
    }

    public async Task<CompanyPortalEmployeesResult> ListEmployeesAsync(Guid companyId, CancellationToken cancellationToken)
    {
        if (!await CompanyExistsAsync(companyId, cancellationToken))
        {
            return CompanyPortalEmployeesResult.NotFound();
        }

        var employees = await db.Providers
            .AsNoTracking()
            .Where(provider => provider.CompanyId == companyId && provider.Status != ProviderStatus.Inactive)
            .OrderBy(provider => provider.LastName)
            .ThenBy(provider => provider.FirstName)
            .Select(provider => new CompanyEmployeeResponse(
                provider.Id,
                provider.FirstName,
                provider.LastName,
                provider.PhoneNumber,
                provider.Email,
                provider.DateOfBirth,
                provider.Address,
                provider.Gender.ToString(),
                provider.EmploymentType.ToString(),
                provider.EmploymentType == ProviderEmploymentType.TemporaryWorker,
                provider.YearsOfExperience,
                provider.Status.ToString(),
                provider.IsAvailable,
                provider.MissionLatitude ?? provider.CurrentLatitude,
                provider.MissionLongitude ?? provider.CurrentLongitude,
                provider.MissionRadiusKm,
                provider.Documents
                    .Where(document => document.DocumentType == ProviderDocumentType.Photo)
                    .OrderByDescending(document => document.CreatedAt)
                    .Select(document => $"/api/company-portal/provider-documents/{document.Id}/preview")
                    .FirstOrDefault(),
                provider.Documents
                    .Where(document => document.DocumentType == ProviderDocumentType.IdentityDocument)
                    .OrderByDescending(document => document.CreatedAt)
                    .Select(document => $"/api/company-portal/provider-documents/{document.Id}/preview")
                    .FirstOrDefault(),
                provider.Documents
                    .Where(document => document.DocumentType == ProviderDocumentType.Diploma)
                    .OrderByDescending(document => document.CreatedAt)
                    .Select(document => $"/api/company-portal/provider-documents/{document.Id}/preview")
                    .FirstOrDefault(),
                provider.Documents.Any(document => document.DocumentType == ProviderDocumentType.Diploma),
                provider.Services
                    .Where(providerService => providerService.IsActive)
                    .OrderBy(providerService => providerService.Service!.Name)
                    .Select(providerService => new CompanyEmployeeServiceResponse(
                        providerService.ServiceId,
                        providerService.Service!.Name,
                        providerService.ExperienceLevel.ToString(),
                        providerService.YearsOfExperience,
                        providerService.PriceTier.ToString(),
                        providerService.Service.NormalPriceAmount,
                        providerService.Service.PremiumPriceAmount,
                        providerService.Service.Currency,
                        providerService.IsActive,
                        providerService.Prestations
                            .Where(prestation => prestation.IsActive)
                            .OrderBy(prestation => prestation.ServicePrestation!.SortOrder)
                            .ThenBy(prestation => prestation.ServicePrestation!.Name)
                            .Select(prestation => new CompanyEmployeeServicePrestationResponse(
                                prestation.ServicePrestationId,
                                prestation.ServicePrestation!.Name,
                                prestation.ServicePrestation.NormalPriceAmount,
                                prestation.ServicePrestation.PremiumPriceAmount,
                                prestation.ServicePrestation.Currency,
                                prestation.IsActive,
                                prestation.ServicePrestation.PriceMinAmount,
                                prestation.ServicePrestation.PriceMaxAmount))
                            .ToList(),
                        providerService.Service.PriceMinAmount,
                        providerService.Service.PriceMaxAmount))
                    .ToList(),
                provider.Documents
                    .OrderBy(document => document.DocumentType)
                    .ThenByDescending(document => document.CreatedAt)
                    .Select(document => new CompanyEmployeeDocumentResponse(
                        document.Id,
                        document.DocumentType.ToString(),
                        document.OriginalFileName,
                        document.ContentType,
                        $"/api/company-portal/provider-documents/{document.Id}/preview",
                        document.CreatedAt))
                    .ToList(),
                0,
                null,
                provider.CreatedAt,
                db.ProviderInvitations
                    .Where(invitation => invitation.ProviderId == provider.Id && invitation.Status == ProviderInvitationStatus.Pending)
                    .OrderByDescending(invitation => invitation.CreatedAt)
                    .Select(invitation => invitation.Code)
                    .FirstOrDefault(),
                db.ProviderInvitations
                    .Where(invitation => invitation.ProviderId == provider.Id && invitation.Status == ProviderInvitationStatus.Pending)
                    .OrderByDescending(invitation => invitation.CreatedAt)
                    .Select(invitation => invitation.InvitationLink)
                    .FirstOrDefault(),
                db.ProviderInvitations
                    .Where(invitation => invitation.ProviderId == provider.Id && invitation.Status == ProviderInvitationStatus.Pending)
                    .OrderByDescending(invitation => invitation.CreatedAt)
                    .Select(invitation => (DateTimeOffset?)invitation.ExpiresAt)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        if (employees.Count == 0)
        {
            return CompanyPortalEmployeesResult.Ok(employees);
        }

        var providerIds = employees.Select(employee => employee.Id).ToHashSet();
        var missionRows = await (from mission in db.Missions.AsNoTracking()
                                 where mission.CompanyId == companyId
                                     && mission.ProviderId != null
                                     && providerIds.Contains(mission.ProviderId.Value)
                                 join service in db.Services.AsNoTracking() on mission.ServiceId equals service.Id
                                 join customer in db.Customers.AsNoTracking() on mission.CustomerId equals customer.Id
                                 select new EmployeeMissionRow(
                                     mission.ProviderId!.Value,
                                     mission.Id,
                                     service.Name,
                                     customer.FirstName + " " + customer.LastName,
                                     mission.ServiceAddress,
                                     mission.ScheduledFor,
                                     mission.Status,
                                     mission.CreatedAt))
            .ToListAsync(cancellationToken);

        var completedMissionCounts = missionRows
            .Where(mission => mission.Status == MissionStatus.Completed)
            .GroupBy(mission => mission.ProviderId)
            .ToDictionary(group => group.Key, group => group.Count());

        var currentMissions = missionRows
            .Where(mission => IsCurrentMissionStatus(mission.Status))
            .OrderBy(mission => mission.ScheduledFor ?? mission.CreatedAt)
            .GroupBy(mission => mission.ProviderId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var mission = group.First();
                    return new CompanyEmployeeCurrentMissionResponse(
                        mission.MissionId,
                        mission.ServiceName,
                        mission.CustomerName,
                        mission.LocationLabel,
                        mission.ScheduledFor,
                        mission.Status.ToString());
                });

        employees = employees
            .Select(employee => employee with
            {
                CompletedMissionCount = completedMissionCounts.GetValueOrDefault(employee.Id),
                CurrentMission = currentMissions.GetValueOrDefault(employee.Id),
                IsAvailable = employee.IsAvailable && !currentMissions.ContainsKey(employee.Id)
            })
            .ToList();

        return CompanyPortalEmployeesResult.Ok(employees);
    }

    public async Task<CompanyPortalPaymentsResult> GetPaymentsAsync(Guid companyId, string? period, CancellationToken cancellationToken)
    {
        if (!await CompanyExistsAsync(companyId, cancellationToken))
        {
            return CompanyPortalPaymentsResult.NotFound();
        }

        var normalizedPeriod = period?.Trim().ToLowerInvariant() ?? "month";
        var start = PaymentPeriodCalculator.GetStart(normalizedPeriod, DateTimeOffset.UtcNow);
        var missions = await (from mission in db.Missions.AsNoTracking()
                              where mission.CompanyId == companyId
                                  && mission.Status == MissionStatus.Completed
                                  && (mission.ScheduledFor == null || mission.ScheduledFor >= start)
                              join service in db.Services.AsNoTracking() on mission.ServiceId equals service.Id
                              join customer in db.Customers.AsNoTracking() on mission.CustomerId equals customer.Id
                              join provider in db.Providers.AsNoTracking() on mission.ProviderId equals provider.Id into providerJoin
                              from provider in providerJoin.DefaultIfEmpty()
                              orderby mission.ScheduledFor descending
                              select new CompanyPortalMissionResponse(
                                  mission.Id,
                                  mission.MissionNumber,
                                  service.Name,
                                  customer.FirstName + " " + customer.LastName,
                                  string.Empty,
                                  mission.Mode.ToString(),
                                  mission.Status.ToString(),
                                  mission.PaymentMethod.ToString(),
                                  mission.PaymentStatus.ToString(),
                                  mission.ScheduledFor,
                                  mission.EstimatedDurationMinutes,
                                  mission.FinalTotalAmount ?? mission.EstimatedTotalAmount,
                                  mission.Currency,
                                  mission.ProviderId,
                                  provider == null ? null : provider.FirstName + " " + provider.LastName,
                                  mission.CompanyQuotedAmount,
                                  mission.CompanyQuoteJustification,
                                  mission.CompanyQuotedAt,
                                  mission.CustomerQuoteAcceptedAt,
                                  service.IconName,
                                  null,
                                  mission.ActualDurationMinutes,
                                  null,
                                  mission.Status == MissionStatus.Cancelled
                                      ? (mission.CancellationFeeAmount > 0 ? "Annulation apres confirmation client" : "Annulation sans frais")
                                      : null,
                                  mission.PlatformCommissionAmount,
                                  mission.CompanyAssignmentExpiresAt,
                                  null,
                                  null,
                                  null,
                                  null,
                                  0))
            .ToListAsync(cancellationToken);

        var financialRows = await db.Missions
            .AsNoTracking()
            .Where(mission => mission.CompanyId == companyId
                && mission.Status == MissionStatus.Completed
                && (mission.ScheduledFor == null || mission.ScheduledFor >= start))
            .Select(mission => new
            {
                mission.Id,
                mission.CustomerId,
                GrossServiceAmount = mission.CompanyQuotedAmount ?? mission.FinalTotalAmount ?? mission.EstimatedTotalAmount ?? 0,
                CommissionRateBasisPoints = mission.PlatformCommissionRateBasisPoints,
                CommissionAmount = mission.PlatformCommissionAmount,
                CompanyNetAmount = mission.CompanyPayoutAmount,
                mission.CommissionableAmount,
                mission.IsFirstCustomerCompanyOrder,
                PartsAmount = mission.PartsEstimateAmount ?? 0,
                mission.CompanyCommissionTierName,
                mission.CompanyCommissionMissionSequence
            })
            .ToListAsync(cancellationToken);

        // Les missions creees avant l'ajout de l'indicateur commercial ont recu
        // `false` par defaut. On reconstitue uniquement leur libelle historique
        // a partir de la premiere relation confirmee, sans modifier les montants.
        var displayedCustomerIds = financialRows
            .Select(item => item.CustomerId)
            .Distinct()
            .ToArray();
        var relationshipHistory = await db.Missions
            .AsNoTracking()
            .Where(mission => mission.CompanyId == companyId
                && displayedCustomerIds.Contains(mission.CustomerId)
                && (mission.CustomerConfirmedAt != null
                    || mission.PaymentStatus == PaymentStatus.Paid
                    || mission.Status == MissionStatus.Completed))
            .Select(mission => new
            {
                mission.Id,
                mission.CustomerId,
                mission.CustomerConfirmedAt,
                mission.CustomerQuoteAcceptedAt,
                mission.CreatedAt
            })
            .ToListAsync(cancellationToken);
        var firstRelationshipMissionIds = relationshipHistory
            .GroupBy(item => item.CustomerId)
            .Select(group => group
                .OrderBy(item => item.CustomerConfirmedAt
                    ?? item.CustomerQuoteAcceptedAt
                    ?? item.CreatedAt)
                .ThenBy(item => item.CreatedAt)
                .ThenBy(item => item.Id)
                .First()
                .Id)
            .ToHashSet();
        var financialBreakdowns = financialRows
            .Select(item => new CompanyPortalPaymentBreakdownResponse(
                item.Id,
                item.GrossServiceAmount,
                item.CommissionRateBasisPoints,
                item.CommissionAmount,
                item.CompanyNetAmount,
                item.CommissionableAmount,
                item.IsFirstCustomerCompanyOrder || firstRelationshipMissionIds.Contains(item.Id),
                item.PartsAmount,
                item.CompanyCommissionTierName,
                item.CompanyCommissionMissionSequence))
            .ToList();

        var paidMissions = missions
            .Where(mission => mission.PaymentStatus == PaymentStatus.Paid.ToString())
            .ToList();
        var pendingMissions = missions
            .Where(mission => mission.PaymentStatus is nameof(PaymentStatus.Pending) or nameof(PaymentStatus.Authorized))
            .ToList();
        var paidMissionIds = paidMissions.Select(mission => mission.Id).ToHashSet();
        var paidFinancialBreakdowns = financialBreakdowns
            .Where(item => paidMissionIds.Contains(item.MissionId))
            .ToList();
        var commissionProgress = await new MissionCommercialPricingService(db)
            .GetCompanyCommissionProgressAsync(companyId, cancellationToken);

        return CompanyPortalPaymentsResult.Ok(new CompanyPortalPaymentSummaryResponse(
            normalizedPeriod,
            paidMissions.Sum(mission => mission.FinalTotalAmount ?? 0),
            paidMissions.Where(mission => mission.PaymentMethod == PaymentMethod.MobileMoney.ToString()).Sum(mission => mission.FinalTotalAmount ?? 0),
            paidMissions.Where(mission => mission.PaymentMethod == PaymentMethod.Card.ToString()).Sum(mission => mission.FinalTotalAmount ?? 0),
            paidMissions.Where(mission => mission.PaymentMethod == PaymentMethod.Cash.ToString()).Sum(mission => mission.FinalTotalAmount ?? 0),
            pendingMissions.Where(mission => mission.PaymentMethod == PaymentMethod.Cash.ToString()).Sum(mission => mission.FinalTotalAmount ?? 0),
            paidMissions.Sum(mission => mission.PlatformCommissionAmount),
            missions.Count,
            "XOF",
            missions,
            paidFinancialBreakdowns.Sum(item => item.GrossServiceAmount),
            paidFinancialBreakdowns.Sum(item => item.CommissionAmount),
            paidFinancialBreakdowns.Sum(item => item.CompanyNetAmount > 0
                ? item.CompanyNetAmount
                : Math.Max(0, item.GrossServiceAmount - item.CommissionAmount)),
            financialBreakdowns,
            new CompanyPortalCommissionProgressResponse(
                commissionProgress.CurrentTierName,
                commissionProgress.CurrentRateBasisPoints,
                commissionProgress.CompletedMissionCount,
                commissionProgress.NextTierMinimumMissionCount,
                commissionProgress.NextTierName,
                commissionProgress.MissionsUntilNextTier,
                commissionProgress.RatingCount,
                commissionProgress.AverageRating,
                commissionProgress.CompanyCancellationRateBasisPoints,
                commissionProgress.DocumentsCompliant,
                commissionProgress.HasOpenDispute,
                commissionProgress.IsQualityEligible)));
    }

    private async Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken cancellationToken)
    {
        return await db.Companies.AnyAsync(company => company.Id == companyId && company.Status != CompanyStatus.Suspended, cancellationToken);
    }

    private static CompanyPortalMissionResponse HideClosedMissionCustomerContact(CompanyPortalMissionResponse mission)
    {
        if (!Enum.TryParse<MissionStatus>(mission.Status, true, out var status)
            || MissionCustomerContactAccessPolicy.CanAccess(status))
        {
            return mission;
        }

        return mission with
        {
            CustomerPhoneNumber = string.Empty,
            LocationLabel = null,
            ServiceLatitude = null,
            ServiceLongitude = null
        };
    }

    private static string GetCompanyDocumentLabel(CompanyDocumentType documentType)
    {
        return documentType switch
        {
            CompanyDocumentType.FiscalExistenceDeclaration => "DFE",
            CompanyDocumentType.BusinessRegistration => "Registre de commerce",
            CompanyDocumentType.OwnerIdentity => "Identite du responsable",
            CompanyDocumentType.AddressProof => "Justificatif d'adresse",
            _ => "Document complementaire"
        };
    }

    private static bool IsCurrentMissionStatus(MissionStatus status)
    {
        return status is MissionStatus.Assigned
            or MissionStatus.Accepted
            or MissionStatus.OnTheWay
            or MissionStatus.Started;
    }

    private static double? CalculateDistanceKilometers(
        decimal? destinationLatitude,
        decimal? destinationLongitude,
        decimal? providerLatitude,
        decimal? providerLongitude)
    {
        if (!destinationLatitude.HasValue || !destinationLongitude.HasValue
            || !providerLatitude.HasValue || !providerLongitude.HasValue)
        {
            return null;
        }

        const double earthRadiusKilometers = 6371.0088;
        var latitude1 = DegreesToRadians((double)providerLatitude.Value);
        var latitude2 = DegreesToRadians((double)destinationLatitude.Value);
        var deltaLatitude = latitude2 - latitude1;
        var deltaLongitude = DegreesToRadians((double)destinationLongitude.Value - (double)providerLongitude.Value);
        var haversine = Math.Pow(Math.Sin(deltaLatitude / 2), 2)
            + Math.Cos(latitude1) * Math.Cos(latitude2) * Math.Pow(Math.Sin(deltaLongitude / 2), 2);
        return Math.Round(earthRadiusKilometers * 2 * Math.Atan2(Math.Sqrt(haversine), Math.Sqrt(1 - haversine)), 2);
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;
}

internal sealed record EmployeeMissionRow(
    Guid ProviderId,
    Guid MissionId,
    string ServiceName,
    string CustomerName,
    string? LocationLabel,
    DateTimeOffset? ScheduledFor,
    MissionStatus Status,
    DateTimeOffset CreatedAt);

public sealed record CompanyPortalProfileResult(bool IsSuccess, CompanyPortalProfileResponse? Response, string? Message)
{
    public static CompanyPortalProfileResult Ok(CompanyPortalProfileResponse response) => new(true, response, null);
    public static CompanyPortalProfileResult NotFound() => new(false, null, "Entreprise introuvable ou inactive.");
}

public sealed record CompanyPortalMissionsResult(bool IsSuccess, IReadOnlyList<CompanyPortalMissionResponse> Missions, string? Message)
{
    public static CompanyPortalMissionsResult Ok(IReadOnlyList<CompanyPortalMissionResponse> missions) => new(true, missions, null);
    public static CompanyPortalMissionsResult NotFound() => new(false, [], "Entreprise introuvable ou inactive.");
}

public sealed record CompanyPortalMissionDetailResult(
    bool IsSuccess,
    CompanyPortalMissionDetailResponse? Response,
    string? Message)
{
    public static CompanyPortalMissionDetailResult Ok(CompanyPortalMissionDetailResponse response)
        => new(true, response, null);

    public static CompanyPortalMissionDetailResult NotFound()
        => new(false, null, "Mission introuvable ou non accessible a cette entreprise.");
}

public sealed record CompanyPortalEmployeesResult(bool IsSuccess, IReadOnlyList<CompanyEmployeeResponse> Employees, string? Message)
{
    public static CompanyPortalEmployeesResult Ok(IReadOnlyList<CompanyEmployeeResponse> employees) => new(true, employees, null);
    public static CompanyPortalEmployeesResult NotFound() => new(false, [], "Entreprise introuvable ou inactive.");
}

public sealed record CompanyPortalPaymentsResult(bool IsSuccess, CompanyPortalPaymentSummaryResponse? Summary, string? Message)
{
    public static CompanyPortalPaymentsResult Ok(CompanyPortalPaymentSummaryResponse summary) => new(true, summary, null);
    public static CompanyPortalPaymentsResult NotFound() => new(false, null, "Entreprise introuvable ou inactive.");
}
