using HomeService.Api;
using HomeService.Api.Endpoints;
using HomeService.Application;
using HomeService.Application.Abstractions;
using HomeService.Application.Clients;
using HomeService.Contracts.Clients;
using HomeService.Infrastructure.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HomeService.Tests.Integration;

public sealed class ProviderPortalEndpointContractTests
{
    [Theory]
    [InlineData("GET", "/api/provider-portal/invitations/{code}")]
    [InlineData("POST", "/api/provider-portal/activate")]
    [InlineData("POST", "/api/provider-portal/login")]
    [InlineData("GET", "/api/provider-portal/me")]
    public void ProviderAuthRoutes_AreMapped(string httpMethod, string routePattern)
    {
        var endpoints = BuildProviderEndpoints();

        Assert.Contains(endpoints, endpoint =>
            endpoint.RoutePattern.RawText == routePattern
            && endpoint.Metadata
                .OfType<HttpMethodMetadata>()
                .Any(metadata => metadata.HttpMethods.Contains(httpMethod)));
    }

    [Theory]
    [InlineData("POST", "/api/provider-portal/mobile/device-token")]
    [InlineData("GET", "/api/provider-portal/mobile/home")]
    [InlineData("GET", "/api/provider-portal/mobile/profile")]
    [InlineData("PUT", "/api/provider-portal/mobile/profile")]
    [InlineData("GET", "/api/provider-portal/mobile/addresses/autocomplete")]
    [InlineData("GET", "/api/provider-portal/mobile/addresses/places/{placeId}")]
    [InlineData("GET", "/api/provider-portal/mobile/profile/documents/{documentId:guid}/preview")]
    [InlineData("GET", "/api/provider-portal/mobile/mission-assignments/{assignmentId:guid}")]
    [InlineData("GET", "/api/provider-portal/mobile/mission-assignments/{assignmentId:guid}/messages")]
    [InlineData("POST", "/api/provider-portal/mobile/mission-assignments/{assignmentId:guid}/messages")]
    [InlineData("POST", "/api/provider-portal/mobile/mission-assignments/{assignmentId:guid}/accept")]
    [InlineData("POST", "/api/provider-portal/mobile/mission-assignments/{assignmentId:guid}/location")]
    [InlineData("POST", "/api/provider-portal/mobile/mission-assignments/{assignmentId:guid}/refuse")]
    [InlineData("POST", "/api/provider-portal/mobile/mission-assignments/{assignmentId:guid}/verify-arrival")]
    [InlineData("POST", "/api/provider-portal/mobile/mission-assignments/{assignmentId:guid}/start")]
    [InlineData("POST", "/api/provider-portal/mobile/mission-assignments/{assignmentId:guid}/complete")]
    [InlineData("POST", "/api/provider-portal/mobile/mission-assignments/{assignmentId:guid}/cancel")]
    [InlineData("POST", "/api/provider-portal/mobile/mission-assignments/{assignmentId:guid}/additional-quotes/request")]
    public void ProviderMobileRoutes_AreMapped(string httpMethod, string routePattern)
    {
        var endpoints = BuildProviderEndpoints();

        Assert.Contains(endpoints, endpoint =>
            endpoint.RoutePattern.RawText == routePattern
            && endpoint.Metadata
                .OfType<HttpMethodMetadata>()
                .Any(metadata => metadata.HttpMethods.Contains(httpMethod)));
    }

    private static IReadOnlyList<RouteEndpoint> BuildProviderEndpoints()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddApplicationServices();
        builder.Services.AddApiStorageServices();
        builder.Services.AddDbContext<HomeServiceDbContext>(options =>
            options.UseInMemoryDatabase($"provider-endpoint-contract-{Guid.NewGuid():N}"));
        builder.Services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<HomeServiceDbContext>());
        builder.Services.AddScoped<IAddressAutocompleteService, StubAddressAutocompleteService>();

        var app = builder.Build();
        app.MapProviderPortalEndpoints();

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
