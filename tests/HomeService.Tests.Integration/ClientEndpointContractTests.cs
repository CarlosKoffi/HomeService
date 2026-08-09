using HomeService.Api;
using HomeService.Api.Endpoints;
using HomeService.Application;
using HomeService.Application.Abstractions;
using HomeService.Application.Clients;
using HomeService.Contracts.Clients;
using HomeService.Infrastructure.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HomeService.Tests.Integration;

public sealed class ClientEndpointContractTests
{
    [Theory]
    [InlineData("POST", "/api/client/auth/register")]
    [InlineData("POST", "/api/client/auth/login")]
    [InlineData("POST", "/api/client/auth/logout")]
    [InlineData("GET", "/api/client/me")]
    [InlineData("PUT", "/api/client/me")]
    [InlineData("GET", "/api/client/catalog/search")]
    [InlineData("GET", "/api/client/missions")]
    [InlineData("POST", "/api/client/mission-photos")]
    [InlineData("POST", "/api/client/missions")]
    [InlineData("GET", "/api/client/missions/{missionId:guid}")]
    [InlineData("GET", "/api/client/missions/{missionId:guid}/messages")]
    [InlineData("POST", "/api/client/missions/{missionId:guid}/messages")]
    [InlineData("POST", "/api/client/missions/{missionId:guid}/confirm")]
    [InlineData("PUT", "/api/client/missions/{missionId:guid}/payment-method")]
    [InlineData("POST", "/api/client/missions/{missionId:guid}/cancel")]
    [InlineData("POST", "/api/client/missions/{missionId:guid}/validate-completion")]
    [InlineData("POST", "/api/client/missions/{missionId:guid}/additional-quotes/{quoteId:guid}/pay")]
    [InlineData("GET", "/api/client/addresses")]
    [InlineData("POST", "/api/client/addresses")]
    [InlineData("PUT", "/api/client/addresses/{addressId:guid}")]
    [InlineData("DELETE", "/api/client/addresses/{addressId:guid}")]
    [InlineData("GET", "/api/client/payment-methods")]
    [InlineData("POST", "/api/client/payment-methods")]
    [InlineData("POST", "/api/client/payment-methods/mobile-money")]
    [InlineData("PUT", "/api/client/payment-methods/mobile-money/{paymentMethodId:guid}")]
    [InlineData("DELETE", "/api/client/payment-methods/{paymentMethodId:guid}")]
    [InlineData("POST", "/api/client/mobile/device-token")]
    public void ClientMissionRoutes_AreMapped(string httpMethod, string routePattern)
    {
        var endpoints = BuildPublicEndpoints();

        Assert.Contains(endpoints, endpoint =>
            endpoint.RoutePattern.RawText == routePattern
            && endpoint.Metadata
                .OfType<HttpMethodMetadata>()
                .Any(metadata => metadata.HttpMethods.Contains(httpMethod)));
    }

    [Theory]
    [InlineData("GET", "/api/services")]
    [InlineData("GET", "/api/cms/client/home")]
    [InlineData("GET", "/api/cms/company/home")]
    [InlineData("GET", "/api/cms/provider/home")]
    [InlineData("GET", "/api/cms/media/{id:guid}")]
    [InlineData("POST", "/api/contact-requests")]
    public void PublicSupportRoutes_AreMapped(string httpMethod, string routePattern)
    {
        var endpoints = BuildPublicEndpoints();

        Assert.Contains(endpoints, endpoint =>
            endpoint.RoutePattern.RawText == routePattern
            && endpoint.Metadata
                .OfType<HttpMethodMetadata>()
                .Any(metadata => metadata.HttpMethods.Contains(httpMethod)));
    }

    [Theory]
    [InlineData("/api/client/auth/register")]
    [InlineData("/api/client/auth/login")]
    [InlineData("/api/company-applications")]
    public void PublicAuthenticationRoutes_AreRateLimited(string routePattern)
    {
        var endpoint = Assert.Single(BuildPublicEndpoints(), endpoint => endpoint.RoutePattern.RawText == routePattern);

        var rateLimit = Assert.Single(endpoint.Metadata.OfType<EnableRateLimitingAttribute>());
        Assert.Equal(AuthenticationRateLimitingExtensions.PolicyName, rateLimit.PolicyName);
    }

    private static IReadOnlyList<RouteEndpoint> BuildPublicEndpoints()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddApplicationServices();
        builder.Services.AddApiStorageServices();
        builder.Services.AddDbContext<HomeServiceDbContext>(options =>
            options.UseInMemoryDatabase($"client-endpoint-contract-{Guid.NewGuid():N}"));
        builder.Services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<HomeServiceDbContext>());
        builder.Services.AddScoped<IAddressAutocompleteService, StubAddressAutocompleteService>();

        var app = builder.Build();
        app.MapPublicEndpoints();

        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();
    }

    private sealed class StubAddressAutocompleteService : IAddressAutocompleteService
    {
        public Task<IReadOnlyList<ClientAddressSuggestionResponse>> SearchAsync(
            string query,
            string? sessionToken,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ClientAddressSuggestionResponse>>([]);

        public Task<ClientPlaceDetailsResponse?> GetDetailsAsync(
            string placeId,
            string? sessionToken,
            CancellationToken cancellationToken)
            => Task.FromResult<ClientPlaceDetailsResponse?>(null);
    }
}
