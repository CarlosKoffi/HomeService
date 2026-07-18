using HomeService.Application.ProviderPortal;
using HomeService.Contracts.ProviderPortal;

namespace HomeService.Api.Endpoints;

public static class ProviderOnboardingEndpoints
{
    public static IEndpointRouteBuilder MapProviderOnboardingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/provider-onboarding");

        group.MapGet("/options", async (
            string? search,
            ProviderOnboardingService onboardingService,
            CancellationToken cancellationToken) =>
        {
            var options = await onboardingService.SearchOptionsAsync(search, cancellationToken);
            return Results.Ok(options);
        })
        .WithName("SearchProviderOnboardingOptions");

        group.MapGet("/opportunities", async (
            string? selectionType,
            Guid selectionId,
            string? address,
            ProviderOnboardingService onboardingService,
            CancellationToken cancellationToken) =>
        {
            var opportunities = await onboardingService.SearchOpportunitiesAsync(
                selectionType,
                selectionId,
                address,
                cancellationToken);
            return Results.Ok(opportunities);
        })
        .WithName("SearchProviderOnboardingOpportunities");

        group.MapPost("/self-registration", async (
            ProviderSelfRegistrationRequest request,
            ProviderSelfRegistrationService registrationService,
            CancellationToken cancellationToken) =>
        {
            var result = await registrationService.RegisterAsync(request, cancellationToken);
            if (result.ProviderId == Guid.Empty)
            {
                return Results.BadRequest(new { message = result.Message });
            }

            return Results.Ok(result);
        })
        .WithName("RegisterSelfProviderCandidate");

        group.MapGet("/companies", async (
            string? serviceIds,
            ProviderOnboardingService onboardingService,
            CancellationToken cancellationToken) =>
        {
            var companies = await onboardingService.SearchCompaniesAsync(serviceIds, cancellationToken);
            return Results.Ok(companies);
        })
        .WithName("SearchProviderOnboardingCompanies");

        group.MapPost("/affiliation-requests", async (
            ProviderAffiliationRequestCreateRequest request,
            ProviderOnboardingService onboardingService,
            CancellationToken cancellationToken) =>
        {
            var result = await onboardingService.CreateAffiliationRequestAsync(request, cancellationToken);
            return result.Status switch
            {
                ProviderAffiliationRequestStatusCode.Success => Results.Ok(result.Response),
                ProviderAffiliationRequestStatusCode.NotFound => Results.NotFound(new { message = result.Message }),
                _ => Results.BadRequest(new { message = result.Message })
            };
        })
        .WithName("CreateProviderAffiliationRequest");

        return app;
    }
}
