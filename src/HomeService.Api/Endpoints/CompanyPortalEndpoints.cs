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
