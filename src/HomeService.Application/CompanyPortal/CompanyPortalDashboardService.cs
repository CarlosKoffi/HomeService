using HomeService.Application.Abstractions;
using HomeService.Application.Companies;
using HomeService.Contracts.CompanyPortal;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.CompanyPortal;

public sealed class CompanyPortalDashboardService(IAppDbContext db)
{
    public async Task<CompanyPortalDashboardResult> GetAsync(Guid companyId, Guid? userId, CancellationToken cancellationToken)
    {
        var company = await db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(company => company.Id == companyId && company.Status != CompanyStatus.Suspended, cancellationToken);
        if (company is null)
        {
            return CompanyPortalDashboardResult.NotFound();
        }

        var user = userId is null
            ? await db.CompanyPortalUsers.AsNoTracking().FirstOrDefaultAsync(item => item.CompanyId == companyId && item.IsOwner, cancellationToken)
            : await db.CompanyPortalUsers.AsNoTracking().FirstOrDefaultAsync(item => item.Id == userId && item.CompanyId == companyId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset);
        var providers = await db.Providers
            .AsNoTracking()
            .Where(provider => provider.CompanyId == companyId && provider.Status != ProviderStatus.Inactive)
            .Select(provider => new
            {
                provider.Id,
                provider.FirstName,
                provider.LastName,
                provider.Status,
                provider.IsAvailable,
                provider.EmploymentType,
                HasDiploma = provider.Documents.Any(document => document.DocumentType == ProviderDocumentType.Diploma),
                PhotoUrl = provider.Documents
                    .Where(document => document.DocumentType == ProviderDocumentType.Photo)
                    .OrderByDescending(document => document.CreatedAt)
                    .Select(document => $"/api/company-portal/provider-documents/{document.Id}/preview")
                    .FirstOrDefault(),
                Services = provider.Services
                    .Where(service => service.IsActive)
                    .OrderBy(service => service.Service!.Name)
                    .Select(service => service.Service!.Name)
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var missionRows = await (from mission in db.Missions.AsNoTracking()
                                 where mission.CompanyId == companyId
                                 join service in db.Services.AsNoTracking() on mission.ServiceId equals service.Id
                                 join customer in db.Customers.AsNoTracking() on mission.CustomerId equals customer.Id
                                 join provider in db.Providers.AsNoTracking() on mission.ProviderId equals provider.Id into providerJoin
                                 from provider in providerJoin.DefaultIfEmpty()
                                 select new DashboardMissionRow(
                                     mission.Id,
                                     service.Name,
                                     customer.FirstName + " " + customer.LastName,
                                     customer.PhoneNumber,
                                     mission.Mode.ToString(),
                                     mission.Status,
                                     mission.PaymentMethod,
                                     mission.PaymentStatus.ToString(),
                                     mission.ScheduledFor,
                                     mission.CreatedAt,
                                     mission.EstimatedDurationMinutes,
                                     mission.FinalTotalAmount,
                                     mission.EstimatedTotalAmount,
                                     mission.Currency,
                                     mission.ProviderId,
                                     provider == null ? null : provider.FirstName + " " + provider.LastName,
                                     mission.CompanyQuotedAmount,
                                     mission.CompanyQuoteJustification,
                                     mission.CompanyQuotedAt,
                                     mission.CustomerQuoteAcceptedAt))
            .ToListAsync(cancellationToken);

        var liveStatuses = new[]
        {
            MissionStatus.SearchingProvider,
            MissionStatus.Offered,
            MissionStatus.Assigned,
            MissionStatus.Accepted,
            MissionStatus.OnTheWay,
            MissionStatus.Started
        };

        var nextMission = missionRows
            .Where(row => row.Status != MissionStatus.Completed
                && row.Status != MissionStatus.Cancelled
                && row.ScheduledFor >= now)
            .OrderBy(row => row.ScheduledFor)
            .Select(ToMissionResponse)
            .FirstOrDefault();

        var activities = await db.CompanyPortalActivities
            .AsNoTracking()
            .Where(activity => activity.CompanyId == companyId)
            .OrderByDescending(activity => activity.OccurredAt)
            .Take(8)
            .Select(activity => new CompanyPortalActivityResponse(
                activity.Id,
                activity.Type,
                activity.Title,
                activity.Description,
                activity.Tone,
                activity.OccurredAt,
                activity.IsRead))
            .ToListAsync(cancellationToken);

        var unreadNotificationCount = await db.CompanyPortalNotifications
            .AsNoTracking()
            .CountAsync(notification => notification.CompanyId == companyId && !notification.IsRead, cancellationToken);

        var notifications = await db.CompanyPortalNotifications
            .AsNoTracking()
            .Where(notification => notification.CompanyId == companyId)
            .OrderByDescending(notification => notification.OccurredAt)
            .Take(5)
            .Select(notification => new CompanyPortalNotificationResponse(
                notification.Id,
                notification.Type,
                notification.Title,
                notification.Message,
                notification.Tone,
                notification.ActionUrl,
                notification.OccurredAt,
                notification.IsRead,
                notification.CompanyApplicationDocumentId))
            .ToListAsync(cancellationToken);

        var complianceDocuments = await db.CompanyApplicationDocuments
            .AsNoTracking()
            .Where(document => document.CompanyApplication != null
                && document.CompanyApplication.CompanyId == companyId)
            .Select(document => new
            {
                document.DocumentType,
                document.ReviewStatus,
                document.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var hasRequiredComplianceDocuments = HasRequiredComplianceDocuments(complianceDocuments
            .Select(document => new ComplianceDocumentProgressRow(document.DocumentType, document.ReviewStatus, document.CreatedAt)));

        var progressSteps = BuildProgressSteps(hasRequiredComplianceDocuments, providers.Count, missionRows.Count);

        var response = new CompanyPortalDashboardResponse(
            company.Id,
            company.Name,
            company.Email ?? string.Empty,
            company.Status.ToString(),
            company.AcceptsInterimApplications,
            user?.FullName ?? "Responsable",
            user?.Email ?? company.Email ?? string.Empty,
            GetProfileCompletionPercent(progressSteps),
            progressSteps,
            providers.Count(provider => provider.Status != ProviderStatus.Inactive),
            providers.Count(provider => provider.IsAvailable),
            missionRows.Count(row => row.ScheduledFor >= now && row.Status != MissionStatus.Completed && row.Status != MissionStatus.Cancelled),
            missionRows.Count(row => liveStatuses.Contains(row.Status)),
            missionRows.Count(row => row.Status == MissionStatus.Completed),
            missionRows.Where(row => row.Status == MissionStatus.Completed && (row.ScheduledFor ?? row.CreatedAt) >= monthStart)
                .Sum(row => row.FinalTotalAmount ?? 0),
            missionRows.Where(row => row.Status == MissionStatus.Completed && row.PaymentMethod == PaymentMethod.Cash && (row.ScheduledFor ?? row.CreatedAt) >= monthStart)
                .Sum(row => row.FinalTotalAmount ?? 0),
            "XOF",
            nextMission,
            unreadNotificationCount,
            notifications,
            activities,
            providers
                .OrderByDescending(provider => provider.IsAvailable)
                .ThenBy(provider => provider.LastName)
                .Take(6)
                .Select(provider => new CompanyPortalEmployeeDigestResponse(
                    provider.Id,
                    GetInitials(provider.FirstName, provider.LastName),
                    $"{provider.FirstName} {provider.LastName}".Trim(),
                    provider.Services.FirstOrDefault() ?? provider.EmploymentType.ToString(),
                    provider.Status.ToString(),
                    provider.IsAvailable,
                    provider.HasDiploma,
                    provider.PhotoUrl))
                .ToList());

        return CompanyPortalDashboardResult.Ok(response);
    }

    private static CompanyPortalMissionResponse ToMissionResponse(DashboardMissionRow row)
    {
        return new CompanyPortalMissionResponse(
            row.Id,
            row.ServiceName,
            row.CustomerName,
            row.CustomerPhoneNumber,
            row.Mode,
            row.Status.ToString(),
            row.PaymentMethod.ToString(),
            row.PaymentStatus,
            row.ScheduledFor,
            row.EstimatedDurationMinutes,
            row.FinalTotalAmount ?? row.EstimatedTotalAmount,
            row.Currency,
            row.ProviderId,
            row.ProviderName,
            row.CompanyQuotedAmount,
            row.CompanyQuoteJustification,
            row.CompanyQuotedAt,
            row.CustomerQuoteAcceptedAt,
            null,
            null,
            null,
            null,
            row.Status == MissionStatus.Cancelled ? "Annulation client" : null);
    }

    private static bool HasRequiredComplianceDocuments(IEnumerable<ComplianceDocumentProgressRow> documents)
    {
        var latestUsableDocumentTypes = documents
            .Where(document => document.ReviewStatus is DocumentReviewStatus.Pending or DocumentReviewStatus.Approved)
            .GroupBy(document => document.DocumentType)
            .Select(group => group.OrderByDescending(document => document.CreatedAt).First().DocumentType)
            .ToHashSet();

        return RequiredCompanyDocumentsPolicy.RequiredDocumentTypes.All(latestUsableDocumentTypes.Contains);
    }

    private static int GetProfileCompletionPercent(IReadOnlyList<CompanyPortalProgressStepResponse> progressSteps)
    {
        return progressSteps.Count(step => step.IsDone) * 25;
    }

    private static IReadOnlyList<CompanyPortalProgressStepResponse> BuildProgressSteps(bool hasRequiredComplianceDocuments, int providerCount, int missionCount)
    {
        return
        [
            new CompanyPortalProgressStepResponse("Profil entreprise", true, null, null),
            new CompanyPortalProgressStepResponse("Documents de conformite", hasRequiredComplianceDocuments, "Completer", "company-profile"),
            new CompanyPortalProgressStepResponse("Premier employe ajoute", providerCount > 0, "Ajouter", "employees"),
            new CompanyPortalProgressStepResponse("Premiere mission creee", missionCount > 0, "Voir", "missions")
        ];
    }

    private static string GetInitials(string firstName, string lastName)
    {
        var first = string.IsNullOrWhiteSpace(firstName) ? string.Empty : firstName.Trim()[0].ToString();
        var last = string.IsNullOrWhiteSpace(lastName) ? string.Empty : lastName.Trim()[0].ToString();
        return $"{first}{last}".ToUpperInvariant();
    }
}

internal sealed record DashboardMissionRow(
    Guid Id,
    string ServiceName,
    string CustomerName,
    string CustomerPhoneNumber,
    string Mode,
    MissionStatus Status,
    PaymentMethod PaymentMethod,
    string PaymentStatus,
    DateTimeOffset? ScheduledFor,
    DateTimeOffset CreatedAt,
    int EstimatedDurationMinutes,
    int? FinalTotalAmount,
    int? EstimatedTotalAmount,
    string Currency,
    Guid? ProviderId,
    string? ProviderName,
    int? CompanyQuotedAmount,
    string? CompanyQuoteJustification,
    DateTimeOffset? CompanyQuotedAt,
    DateTimeOffset? CustomerQuoteAcceptedAt);

internal sealed record ComplianceDocumentProgressRow(
    CompanyDocumentType DocumentType,
    DocumentReviewStatus ReviewStatus,
    DateTimeOffset CreatedAt);

public sealed record CompanyPortalDashboardResult(bool IsSuccess, CompanyPortalDashboardResponse? Response, string? Message)
{
    public static CompanyPortalDashboardResult Ok(CompanyPortalDashboardResponse response) => new(true, response, null);
    public static CompanyPortalDashboardResult NotFound() => new(false, null, "Entreprise introuvable ou inactive.");
}
