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

public sealed class ClientEndpointContractTests
{
    [Theory]
    [InlineData("POST", "/api/client/mission-photos")]
    [InlineData("POST", "/api/client/missions")]
    [InlineData("GET", "/api/client/missions/{missionId:guid}")]
    [InlineData("POST", "/api/client/missions/{missionId:guid}/confirm")]
    [InlineData("POST", "/api/client/missions/{missionId:guid}/cancel")]
    [InlineData("POST", "/api/client/missions/{missionId:guid}/validate-completion")]
    [InlineData("POST", "/api/client/missions/{missionId:guid}/additional-quotes/{quoteId:guid}/pay")]
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

    private static IReadOnlyList<RouteEndpoint> BuildPublicEndpoints()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddApplicationServices();
        builder.Services.AddApiStorageServices();
        builder.Services.AddDbContext<HomeServiceDbContext>(options =>
            options.UseInMemoryDatabase($"client-endpoint-contract-{Guid.NewGuid():N}"));
        builder.Services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<HomeServiceDbContext>());

        var app = builder.Build();
        app.MapPublicEndpoints();

        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();
    }
}
