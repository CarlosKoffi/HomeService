using HomeService.Api.Auditing;
using HomeService.Application.Abstractions;
using HomeService.Application.Auditing;
using HomeService.Application.CompanyPortal;
using HomeService.Contracts.CompanyPortal;
using HomeService.Domain.Entities;

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
