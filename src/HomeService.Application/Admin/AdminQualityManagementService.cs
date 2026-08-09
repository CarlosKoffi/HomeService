using HomeService.Application.Abstractions;
using HomeService.Application.Quality;
using HomeService.Contracts.Admin;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminQualityManagementService(IAppDbContext db, QualityScoringService scoringService)
{
    public async Task<AdminQualityDashboardResponse> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var templates = await ListTemplatesAsync(cancellationToken);
        var audits = await ListAuditsAsync(QualityAuditStatus.Pending, 30, cancellationToken);
        var providersAtRisk = await ListProviderScoresAsync(69, 30, cancellationToken);
        var companies = await ListCompanyScoresAsync(30, cancellationToken);
        var stats = new AdminQualityStatsResponse(
            templates.Count(item => item.IsActive),
            await db.MissionQualityAudits.CountAsync(item => item.Status == QualityAuditStatus.Pending, cancellationToken),
            await db.MissionQualityAudits.CountAsync(item => item.Status == QualityAuditStatus.Failed, cancellationToken),
            await db.ProviderPrestationQualifications.CountAsync(item => item.Status == ProviderQualificationStatus.Approved, cancellationToken),
            await db.ProviderPrestationQualifications.CountAsync(item => item.Status == ProviderQualificationStatus.PendingReview, cancellationToken),
            await db.ProviderQualitySummaries.CountAsync(item => item.Level == ProviderQualityLevel.UnderReview || item.Level == ProviderQualityLevel.Suspended, cancellationToken));
        return new AdminQualityDashboardResponse(stats, audits, providersAtRisk, companies, templates);
    }

    public async Task<IReadOnlyList<AdminQualityChecklistTemplateResponse>> ListTemplatesAsync(CancellationToken cancellationToken)
    {
        var templates = await db.QualityChecklistTemplates.AsNoTracking()
            .Include(item => item.Service)
            .Include(item => item.ServicePrestation)
            .Include(item => item.Items).ThenInclude(item => item.ServiceOption)
            .OrderBy(item => item.Service!.Name).ThenBy(item => item.ServicePrestation!.Name).ThenByDescending(item => item.Version)
            .ToListAsync(cancellationToken);
        return templates.Select(MapTemplate).ToList();
    }

    public async Task<AdminQualityChecklistTemplateResponse> CreateTemplateAsync(CreateAdminQualityChecklistTemplateRequest request, CancellationToken cancellationToken)
    {
        var serviceExists = await db.Services.AnyAsync(item => item.Id == request.ServiceId, cancellationToken);
        if (!serviceExists) throw new ArgumentException("Service introuvable.");
        if (request.ServicePrestationId.HasValue && !await db.ServicePrestations.AnyAsync(item => item.Id == request.ServicePrestationId && item.ServiceId == request.ServiceId, cancellationToken))
            throw new ArgumentException("La prestation n'appartient pas au service selectionne.");

        var version = (await db.QualityChecklistTemplates
            .Where(item => item.ServiceId == request.ServiceId && item.ServicePrestationId == request.ServicePrestationId)
            .MaxAsync(item => (int?)item.Version, cancellationToken) ?? 0) + 1;
        var template = new QualityChecklistTemplate(request.ServiceId, request.ServicePrestationId, request.Name, request.Description, version);
        db.QualityChecklistTemplates.Add(template);

        if (request.CopyFromServiceDefault && request.ServicePrestationId.HasValue)
        {
            var source = await db.QualityChecklistTemplates.AsNoTracking().Include(item => item.Items)
                .Where(item => item.ServiceId == request.ServiceId && item.ServicePrestationId == null && item.IsActive)
                .OrderByDescending(item => item.Version).FirstOrDefaultAsync(cancellationToken);
            if (source is not null)
            {
                foreach (var item in source.Items.Where(item => item.IsActive))
                    db.QualityChecklistItems.Add(new QualityChecklistItem(template.Id, item.Code, item.Label, item.Stage, item.ResponseType, item.IsRequired, item.SortOrder, item.Guidance, item.ServiceOptionId, item.RequiresEvidenceOnIssue));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return (await ListTemplatesAsync(cancellationToken)).First(item => item.Id == template.Id);
    }

    public async Task<AdminQualityChecklistTemplateResponse?> UpdateTemplateAsync(Guid id, UpdateAdminQualityChecklistTemplateRequest request, CancellationToken cancellationToken)
    {
        var template = await db.QualityChecklistTemplates.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (template is null) return null;
        template.Update(request.Name, request.Description);
        if (request.IsActive) template.Activate(); else template.Deactivate();
        await db.SaveChangesAsync(cancellationToken);
        return (await ListTemplatesAsync(cancellationToken)).First(item => item.Id == id);
    }

    public async Task<AdminQualityChecklistItemResponse?> AddItemAsync(Guid templateId, CreateAdminQualityChecklistItemRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<QualityChecklistStage>(request.Stage, true, out var stage)) throw new ArgumentException("Etape de checklist invalide.");
        if (!Enum.TryParse<QualityChecklistResponseType>(request.ResponseType, true, out var responseType)) throw new ArgumentException("Type de reponse invalide.");
        if (!await db.QualityChecklistTemplates.AnyAsync(item => item.Id == templateId, cancellationToken)) return null;
        if (await db.QualityChecklistItems.AnyAsync(item => item.TemplateId == templateId && item.Code == request.Code.Trim().ToLower(), cancellationToken))
            throw new InvalidOperationException("Ce code de controle existe deja dans la checklist.");
        var entity = new QualityChecklistItem(templateId, request.Code, request.Label, stage, responseType, request.IsRequired, request.SortOrder, request.Guidance, request.ServiceOptionId, request.RequiresEvidenceOnIssue);
        db.QualityChecklistItems.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return (await ListTemplatesAsync(cancellationToken)).SelectMany(item => item.Items).First(item => item.Id == entity.Id);
    }

    public async Task<AdminQualityChecklistItemResponse?> UpdateItemAsync(Guid itemId, UpdateAdminQualityChecklistItemRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<QualityChecklistStage>(request.Stage, true, out var stage)) throw new ArgumentException("Etape de checklist invalide.");
        if (!Enum.TryParse<QualityChecklistResponseType>(request.ResponseType, true, out var responseType)) throw new ArgumentException("Type de reponse invalide.");
        var item = await db.QualityChecklistItems.FirstOrDefaultAsync(candidate => candidate.Id == itemId, cancellationToken);
        if (item is null) return null;
        item.Update(request.Label, request.Guidance, stage, responseType, request.IsRequired, request.RequiresEvidenceOnIssue, request.SortOrder);
        item.SetActive(request.IsActive);
        await db.SaveChangesAsync(cancellationToken);
        return (await ListTemplatesAsync(cancellationToken)).SelectMany(template => template.Items).First(candidate => candidate.Id == itemId);
    }

    public async Task<IReadOnlyList<AdminProviderQualificationResponse>> ListQualificationsAsync(ProviderQualificationStatus? status, CancellationToken cancellationToken)
    {
        var query = from qualification in db.ProviderPrestationQualifications.AsNoTracking()
                    join provider in db.Providers.AsNoTracking() on qualification.ProviderId equals provider.Id
                    join prestation in db.ServicePrestations.AsNoTracking() on qualification.ServicePrestationId equals prestation.Id
                    join service in db.Services.AsNoTracking() on prestation.ServiceId equals service.Id
                    where status == null || qualification.Status == status
                    orderby qualification.Status, provider.LastName, provider.FirstName, prestation.Name
                    select new AdminProviderQualificationResponse(qualification.Id, provider.Id, provider.FirstName + " " + provider.LastName,
                        service.Id, service.Name, prestation.Id, prestation.Name, qualification.Status.ToString(), qualification.TheoryScore,
                        qualification.PracticalScore, qualification.ReviewNote, qualification.ReviewedAt, qualification.ExpiresAt);
        return await query.Take(300).ToListAsync(cancellationToken);
    }

    public async Task<AdminProviderQualificationResponse?> ReviewQualificationAsync(Guid id, ReviewAdminProviderQualificationRequest request, Guid adminUserId, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ProviderQualificationStatus>(request.Status, true, out var status)) throw new ArgumentException("Statut de qualification invalide.");
        var entity = await db.ProviderPrestationQualifications.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) return null;
        entity.Review(status, request.TheoryScore, request.PracticalScore, request.ReviewNote, adminUserId, request.ExpiresAt);
        await db.SaveChangesAsync(cancellationToken);
        return (await ListQualificationsAsync(null, cancellationToken)).First(item => item.Id == id);
    }

    public async Task<IReadOnlyList<AdminQualityAuditResponse>> ListAuditsAsync(QualityAuditStatus? status, int take, CancellationToken cancellationToken)
    {
        var raw = await (from audit in db.MissionQualityAudits.AsNoTracking()
                         join mission in db.Missions.AsNoTracking() on audit.MissionId equals mission.Id
                         join provider in db.Providers.AsNoTracking() on audit.ProviderId equals provider.Id
                         join company in db.Companies.AsNoTracking() on audit.CompanyId equals company.Id
                         join service in db.Services.AsNoTracking() on audit.ServiceId equals service.Id
                         join prestation0 in db.ServicePrestations.AsNoTracking() on audit.ServicePrestationId equals (Guid?)prestation0.Id into prestations
                         from prestation in prestations.DefaultIfEmpty()
                         where status == null || audit.Status == status
                         orderby audit.CreatedAt descending
                         select new { audit, mission.MissionNumber, ProviderName = provider.FirstName + " " + provider.LastName, CompanyName = company.Name, ServiceName = service.Name, PrestationName = prestation == null ? null : prestation.Name })
            .Take(Math.Clamp(take, 1, 300)).ToListAsync(cancellationToken);
        var missionIds = raw.Select(item => item.audit.MissionId).ToList();
        var controls = await db.MissionQualityControls.AsNoTracking().Include(item => item.Items).Where(item => missionIds.Contains(item.MissionId)).ToListAsync(cancellationToken);
        return raw.Select(row =>
        {
            var control = controls.FirstOrDefault(item => item.MissionId == row.audit.MissionId);
            return new AdminQualityAuditResponse(row.audit.Id, row.audit.MissionId, row.MissionNumber, row.audit.ProviderId, row.ProviderName,
                row.audit.CompanyId, row.CompanyName, row.ServiceName, row.PrestationName, row.audit.Status.ToString(), row.audit.SamplingReason,
                row.audit.Score, row.audit.ReviewNote, row.audit.CreatedAt, row.audit.ReviewedAt,
                control?.Items.Count(item => item.IsCompleted) ?? 0, control?.Items.Count ?? 0,
                control?.Items.Count(item => item.EvidenceAttachmentId.HasValue) ?? 0);
        }).ToList();
    }

    public async Task<AdminQualityAuditResponse?> ReviewAuditAsync(Guid id, ReviewAdminQualityAuditRequest request, Guid adminUserId, CancellationToken cancellationToken)
    {
        var audit = await db.MissionQualityAudits.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (audit is null) return null;
        audit.Decide(request.Passed, request.Score, request.ReviewNote, adminUserId);
        await scoringService.RecalculateProviderAsync(audit.ProviderId, audit.ServiceId, audit.ServicePrestationId, cancellationToken);
        await scoringService.RecalculateCompanyAsync(audit.CompanyId, audit.ServiceId, audit.ServicePrestationId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return (await ListAuditsAsync(null, 300, cancellationToken)).First(item => item.Id == id);
    }

    private async Task<IReadOnlyList<AdminProviderQualityResponse>> ListProviderScoresAsync(int maximumScore, int take, CancellationToken cancellationToken) =>
        await (from summary in db.ProviderQualitySummaries.AsNoTracking()
               join provider in db.Providers.AsNoTracking() on summary.ProviderId equals provider.Id
               join service in db.Services.AsNoTracking() on summary.ServiceId equals service.Id
               join prestation0 in db.ServicePrestations.AsNoTracking() on summary.ServicePrestationId equals (Guid?)prestation0.Id into prestations
               from prestation in prestations.DefaultIfEmpty()
               where summary.Score <= maximumScore
               orderby summary.Score, provider.LastName
               select new AdminProviderQualityResponse(provider.Id, provider.FirstName + " " + provider.LastName, service.Id, service.Name,
                   summary.ServicePrestationId, prestation == null ? null : prestation.Name, summary.Score, summary.Level.ToString(), summary.CompletedMissionCount,
                   summary.AuditedMissionCount, summary.PassedAuditCount, summary.ConfirmedIncidentCount, summary.AverageRating, summary.PunctualityRate, summary.CalculatedAt))
            .Take(take).ToListAsync(cancellationToken);

    private async Task<IReadOnlyList<AdminCompanyQualityResponse>> ListCompanyScoresAsync(int take, CancellationToken cancellationToken) =>
        await (from summary in db.CompanyQualitySummaries.AsNoTracking()
               join company in db.Companies.AsNoTracking() on summary.CompanyId equals company.Id
               join service in db.Services.AsNoTracking() on summary.ServiceId equals service.Id
               join prestation0 in db.ServicePrestations.AsNoTracking() on summary.ServicePrestationId equals (Guid?)prestation0.Id into prestations
               from prestation in prestations.DefaultIfEmpty()
               orderby summary.Score descending, summary.CompletedMissionCount descending
               select new AdminCompanyQualityResponse(company.Id, company.Name, service.Id, service.Name, summary.ServicePrestationId,
                   prestation == null ? null : prestation.Name, summary.Score, summary.CompletedMissionCount, summary.EligibleProviderCount,
                   summary.AverageRating, summary.AuditPassRate, summary.CalculatedAt))
            .Take(take).ToListAsync(cancellationToken);

    private static AdminQualityChecklistTemplateResponse MapTemplate(QualityChecklistTemplate item) => new(
        item.Id, item.ServiceId, item.Service?.Name ?? string.Empty, item.ServicePrestationId, item.ServicePrestation?.Name,
        item.Name, item.Description, item.Version, item.IsActive,
        item.Items.OrderBy(child => child.Stage).ThenBy(child => child.SortOrder).Select(child => new AdminQualityChecklistItemResponse(
            child.Id, child.Code, child.Label, child.Guidance, child.Stage.ToString(), child.ResponseType.ToString(), child.IsRequired,
            child.RequiresEvidenceOnIssue, child.ServiceOptionId, child.ServiceOption?.Name, child.SortOrder, child.IsActive)).ToList());
}
