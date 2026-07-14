using HomeService.Api.Auditing;
using HomeService.Application.Auditing;
using HomeService.Application.Companies;
using HomeService.Application.Abstractions;
using HomeService.Contracts.Companies;

namespace HomeService.Api.Endpoints;

public static class CompanyActivationEndpoints
{
    public static IEndpointRouteBuilder MapCompanyActivationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/company-activation");

        group.MapGet("/{applicationId:guid}", async (
            Guid applicationId,
            string token,
            CompanyActivationPreviewService previewService,
            CancellationToken cancellationToken) =>
        {
            var result = await previewService.GetPreviewAsync(applicationId, token, cancellationToken);
            return result.Status == CompanyActivationPreviewStatus.Ok
                ? Results.Ok(result.Response)
                : Results.BadRequest(new { message = result.Message });
        })
        .WithName("PreviewCompanyActivation");

        group.MapPost("/password", async (
            CompanyActivationPasswordRequest request,
            HttpRequest httpRequest,
            CompanyActivationPasswordService activationPasswordService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await activationPasswordService.CreatePasswordAsync(request, cancellationToken);
            if (result.Status == CompanyActivationPasswordStatus.ValidationFailed)
            {
                return Results.BadRequest(new { message = result.Message });
            }

            if (result.Status == CompanyActivationPasswordStatus.InvalidOrExpiredToken)
            {
                return Results.BadRequest(new { message = result.Message });
            }

            if (result.Status == CompanyActivationPasswordStatus.DuplicatePortalUser)
            {
                return Results.BadRequest(new { message = result.Message });
            }

            var application = result.Application!;
            var company = result.Company!;
            db.AuditLogEntries.Add(AuditLogFactory.Create(
                AuditActor.Company(company.Id, company.Name),
                "CompanyActivationPasswordCreated",
                nameof(HomeService.Domain.Entities.CompanyApplication),
                application.Id,
                "Mot de passe entreprise cree depuis le lien d'activation.",
                HttpAuditContextFactory.Create(httpRequest),
                before: new { Status = result.PreviousStatus },
                after: new { application.Status, company.Id, result.Email }));
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(result.Response);
        })
        .WithName("CreateCompanyPasswordFromActivationToken");

        return app;
    }
}
