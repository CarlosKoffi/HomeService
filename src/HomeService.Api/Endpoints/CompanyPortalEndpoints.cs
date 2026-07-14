using HomeService.Api.Auditing;
using HomeService.Application.Companies;
using HomeService.Application.Abstractions;
using HomeService.Application.Auditing;
using HomeService.Application.CompanyPortal;
using HomeService.Contracts.CompanyPortal;
using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Api.Endpoints;

public static class CompanyPortalEndpoints
{
    public static IEndpointRouteBuilder MapCompanyPortalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/company-portal");

        group.MapPost("/login", async (
            CompanyPortalLoginRequest request,
            HttpRequest httpRequest,
            CompanyPortalAuthService authService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await authService.LoginAsync(request, cancellationToken);
            var error = CompanyPortalLoginHttpMapper.ToErrorResult(result);
            if (error is not null)
            {
                return error;
            }

            var response = result.Response!;
            db.AuditLogEntries.Add(AuditLogFactory.Create(
                AuditActor.Company(response.CompanyId, response.CompanyName),
                "CompanyPortalLoginSucceeded",
                nameof(CompanyPortalSession),
                result.Session!.Id,
                "Connexion au portail entreprise.",
                HttpAuditContextFactory.Create(httpRequest),
                after: new { response.Email, response.CompanyId, request.RememberMe, response.ExpiresAt }));
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(response);
        })
        .WithName("LoginCompanyPortal");

        group.MapGet("/{companyId:guid}/interim-candidates", async (
            Guid companyId,
            CompanyInterimCandidateService interimCandidateService,
            CancellationToken cancellationToken) =>
        {
            var candidates = await interimCandidateService.ListAsync(companyId, cancellationToken);
            return Results.Ok(candidates);
        })
        .WithName("ListCompanyPortalInterimCandidates");

        group.MapPost("/{companyId:guid}/interim-candidates/{requestId:guid}/approve", async (
            Guid companyId,
            Guid requestId,
            CompanyReviewInterimCandidateRequest request,
            HttpRequest httpRequest,
            CompanyInterimCandidateService interimCandidateService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await interimCandidateService.ApproveAsync(companyId, requestId, request.Note, cancellationToken);
            if (result.IsNotFound)
            {
                return Results.NotFound(new { message = "Demande d'interim introuvable." });
            }

            db.AuditLogEntries.Add(AuditLogFactory.Create(
                AuditActor.Company(companyId, "Entreprise"),
                "InterimCandidateApproved",
                nameof(ProviderAffiliationRequest),
                requestId,
                "Demandeur d'interim valide par l'entreprise.",
                HttpAuditContextFactory.Create(httpRequest),
                after: new { companyId, requestId, request.Note }));
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(new { message = "Candidat valide comme interimaire. Il devient assignable apres rattachement entreprise." });
        })
        .WithName("ApproveCompanyPortalInterimCandidate");

        group.MapPost("/{companyId:guid}/interim-candidates/{requestId:guid}/reject", async (
            Guid companyId,
            Guid requestId,
            CompanyReviewInterimCandidateRequest request,
            HttpRequest httpRequest,
            CompanyInterimCandidateService interimCandidateService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await interimCandidateService.RejectAsync(companyId, requestId, request.Note, cancellationToken);
            if (result.IsNotFound)
            {
                return Results.NotFound(new { message = "Demande d'interim introuvable." });
            }

            db.AuditLogEntries.Add(AuditLogFactory.Create(
                AuditActor.Company(companyId, "Entreprise"),
                "InterimCandidateRejected",
                nameof(ProviderAffiliationRequest),
                requestId,
                "Demandeur d'interim refuse par l'entreprise.",
                HttpAuditContextFactory.Create(httpRequest),
                after: new { companyId, requestId, request.Note }));
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(new { message = "Candidature refusee." });
        })
        .WithName("RejectCompanyPortalInterimCandidate");

        group.MapPost("/{companyId:guid}/compliance-documents", async (
            Guid companyId,
            HttpRequest httpRequest,
            CompanyComplianceDocumentService complianceDocumentService,
            IAppDbContext db,
            CompanyApplicationUploadService uploadService,
            CancellationToken cancellationToken) =>
        {
            if (!httpRequest.HasFormContentType)
            {
                return Results.BadRequest(new { message = "Les documents doivent etre envoyes au format multipart/form-data." });
            }

            var target = await complianceDocumentService.GetUploadTargetAsync(companyId, cancellationToken);
            if (target.Status == CompanyComplianceDocumentStatus.CompanyNotFound)
            {
                return Results.NotFound(new { message = target.Message });
            }

            if (target.Status == CompanyComplianceDocumentStatus.ApplicationNotFound || target.ApplicationId is null)
            {
                return Results.NotFound(new { message = target.Message });
            }

            var form = await httpRequest.ReadFormAsync(cancellationToken);
            var documents = await uploadService.SaveAsync(target.ApplicationId.Value, form.Files, cancellationToken);
            var result = await complianceDocumentService.AttachDocumentsAsync(
                companyId,
                documents.Select(document => new CompanyApplicationUploadedDocument(
                        document.DocumentType,
                        document.OriginalFileName,
                        document.StoragePath,
                        document.ContentType))
                    .ToList(),
                cancellationToken);

            if (result.Status == CompanyComplianceDocumentStatus.NoValidDocument)
            {
                return Results.BadRequest(new { message = result.Message });
            }

            if (result.Status == CompanyComplianceDocumentStatus.CompanyNotFound)
            {
                return Results.NotFound(new { message = result.Message });
            }

            if (result.Status == CompanyComplianceDocumentStatus.ApplicationNotFound || result.ApplicationId is null)
            {
                return Results.NotFound(new { message = result.Message });
            }

            db.AuditLogEntries.Add(AuditLogFactory.Create(
                AuditActor.Company(companyId, null),
                "CompanyComplianceDocumentsUploaded",
                nameof(HomeService.Domain.Entities.CompanyApplication),
                result.ApplicationId.Value,
                "Pieces de conformite ajoutees depuis le portail entreprise.",
                HttpAuditContextFactory.Create(httpRequest),
                after: new { result.DocumentCount, result.DocumentTypes }));
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(new { message = result.Message, count = result.DocumentCount });
        })
        .WithName("UploadCompanyPortalComplianceDocuments");

        group.MapGet("/{companyId:guid}/profile", async (
            Guid companyId,
            CompanyPortalQueryService queryService,
            CancellationToken cancellationToken) =>
        {
            var result = await queryService.GetProfileAsync(companyId, cancellationToken);
            return result.IsSuccess
                ? Results.Ok(result.Response)
                : Results.NotFound(new { message = result.Message });
        })
        .WithName("GetCompanyPortalProfile");

        group.MapGet("/{companyId:guid}/missions", async (
            Guid companyId,
            string? view,
            CompanyPortalQueryService queryService,
            CancellationToken cancellationToken) =>
        {
            var result = await queryService.ListMissionsAsync(companyId, view, cancellationToken);
            return result.IsSuccess
                ? Results.Ok(result.Missions)
                : Results.NotFound(new { message = result.Message });
        })
        .WithName("ListCompanyPortalMissions");

        group.MapGet("/{companyId:guid}/employees", async (
            Guid companyId,
            CompanyPortalQueryService queryService,
            CancellationToken cancellationToken) =>
        {
            var result = await queryService.ListEmployeesAsync(companyId, cancellationToken);
            return result.IsSuccess
                ? Results.Ok(result.Employees)
                : Results.NotFound(new { message = result.Message });
        })
        .WithName("ListCompanyPortalEmployees");

        group.MapGet("/{companyId:guid}/payments", async (
            Guid companyId,
            string? period,
            CompanyPortalQueryService queryService,
            CancellationToken cancellationToken) =>
        {
            var result = await queryService.GetPaymentsAsync(companyId, period, cancellationToken);
            return result.IsSuccess
                ? Results.Ok(result.Summary)
                : Results.NotFound(new { message = result.Message });
        })
        .WithName("GetCompanyPortalPayments");

        group.MapGet("/provider-documents/{id:guid}/preview", async (
            Guid id,
            IAppDbContext db,
            CompanyProviderUploadService uploadService,
            CancellationToken cancellationToken) =>
        {
            var document = await db.ProviderDocuments
                .AsNoTracking()
                .Where(document => document.Id == id)
                .Select(document => new
                {
                    document.OriginalFileName,
                    document.StoragePath,
                    document.ContentType
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (document is null)
            {
                return Results.NotFound();
            }

            var absolutePath = uploadService.GetAbsolutePath(document.StoragePath);
            if (!File.Exists(absolutePath))
            {
                return Results.NotFound(new { message = "Le fichier employe n'existe plus sur le serveur." });
            }

            return Results.File(absolutePath, document.ContentType, document.OriginalFileName, enableRangeProcessing: true);
        })
        .WithName("PreviewCompanyPortalProviderDocument");

        return app;
    }
}

public static class CompanyPortalLoginHttpMapper
{
    public static IResult? ToErrorResult(CompanyPortalLoginResult result)
    {
        return result.Status switch
        {
            CompanyPortalLoginStatus.Ok => null,
            CompanyPortalLoginStatus.MissingCredentials => Results.BadRequest(new { message = result.Message }),
            CompanyPortalLoginStatus.InvalidCredentials => Results.Unauthorized(),
            CompanyPortalLoginStatus.CompanySuspended => Results.BadRequest(new { message = result.Message }),
            _ => Results.BadRequest(new { message = "Connexion impossible." })
        };
    }
}
