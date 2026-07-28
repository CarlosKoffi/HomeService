using HomeService.Api;
using HomeService.Api.Endpoints;
using HomeService.Application;
using HomeService.Application.Abstractions;
using HomeService.Infrastructure.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HomeService.Tests.Integration;

public sealed class ProviderOnboardingEndpointContractTests
{
    [Theory]
    [InlineData("GET", "/api/provider-onboarding/options")]
    [InlineData("GET", "/api/provider-onboarding/opportunities")]
    [InlineData("POST", "/api/provider-onboarding/self-registration")]
    [InlineData("GET", "/api/provider-onboarding/companies")]
    [InlineData("POST", "/api/provider-onboarding/affiliation-requests")]
    public void ProviderOnboardingRoutes_AreMapped(string httpMethod, string routePattern)
    {
        var endpoints = BuildProviderOnboardingEndpoints();

        Assert.Contains(endpoints, endpoint =>
            endpoint.RoutePattern.RawText == routePattern
            && endpoint.Metadata
                .OfType<HttpMethodMetadata>()
                .Any(metadata => metadata.HttpMethods.Contains(httpMethod)));
    }

    private static IReadOnlyList<RouteEndpoint> BuildProviderOnboardingEndpoints()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddApplicationServices();
        builder.Services.AddDbContext<HomeServiceDbContext>(options =>
            options.UseInMemoryDatabase($"provider-onboarding-endpoint-contract-{Guid.NewGuid():N}"));
        builder.Services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<HomeServiceDbContext>());

        var app = builder.Build();
        app.MapProviderOnboardingEndpoints();

        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();
    }
}
