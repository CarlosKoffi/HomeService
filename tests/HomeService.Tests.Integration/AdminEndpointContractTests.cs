using HomeService.Api;
using HomeService.Api.Endpoints;
using HomeService.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace HomeService.Tests.Integration;

public sealed class AdminEndpointContractTests
{
    [Theory]
    [InlineData("GET", "/api/admin/notification-templates")]
    [InlineData("POST", "/api/admin/notification-templates")]
    [InlineData("PUT", "/api/admin/notification-templates/{id:guid}")]
    [InlineData("GET", "/api/admin/notification-delivery-rules")]
    [InlineData("PUT", "/api/admin/notification-delivery-rules/{id:guid}")]
    [InlineData("GET", "/api/admin/notifications")]
    [InlineData("POST", "/api/admin/notifications/{id:guid}/retry")]
    [InlineData("POST", "/api/admin/notifications/{id:guid}/cancel")]
    [InlineData("POST", "/api/admin/notifications/{id:guid}/mark-sent")]
    public void AdminNotificationRoutes_AreMapped(string httpMethod, string routePattern)
    {
        var endpoints = BuildAdminEndpoints();

        Assert.Contains(endpoints, endpoint =>
            endpoint.RoutePattern.RawText == routePattern
            && endpoint.Metadata
                .OfType<HttpMethodMetadata>()
                .Any(metadata => metadata.HttpMethods.Contains(httpMethod)));
    }

    [Theory]
    [InlineData("GET", "/api/admin/missions")]
    [InlineData("GET", "/api/admin/missions/{missionId:guid}")]
    [InlineData("POST", "/api/admin/missions/{missionId:guid}/dispatch-offers")]
    [InlineData("POST", "/api/admin/missions/{missionId:guid}/mark-disputed")]
    [InlineData("POST", "/api/admin/missions/{missionId:guid}/resolve-dispute")]
    [InlineData("POST", "/api/admin/missions/{missionId:guid}/cancel")]
    public void AdminMissionRoutes_AreMapped(string httpMethod, string routePattern)
    {
        var endpoints = BuildAdminEndpoints();

        Assert.Contains(endpoints, endpoint =>
            endpoint.RoutePattern.RawText == routePattern
            && endpoint.Metadata
                .OfType<HttpMethodMetadata>()
                .Any(metadata => metadata.HttpMethods.Contains(httpMethod)));
    }

    private static IReadOnlyList<RouteEndpoint> BuildAdminEndpoints()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddApplicationServices();
        builder.Services.AddApiStorageServices();

        var app = builder.Build();
        app.MapAdminEndpoints();

        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();
    }
}
