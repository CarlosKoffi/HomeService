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

public sealed class CompanyPortalEndpointContractTests
{
    [Theory]
    [InlineData("POST", "/api/company-portal/login")]
    [InlineData("GET", "/api/company-portal/{companyId:guid}/dashboard")]
    [InlineData("GET", "/api/company-portal/{companyId:guid}/notifications")]
    [InlineData("POST", "/api/company-portal/{companyId:guid}/notifications/mark-read")]
    [InlineData("POST", "/api/company-portal/{companyId:guid}/notifications/{notificationId:guid}/mark-read")]
    public void CompanyPortalCoreRoutes_AreMapped(string httpMethod, string routePattern)
    {
        var endpoints = BuildCompanyPortalEndpoints();

        Assert.Contains(endpoints, endpoint =>
            endpoint.RoutePattern.RawText == routePattern
            && endpoint.Metadata
                .OfType<HttpMethodMetadata>()
                .Any(metadata => metadata.HttpMethods.Contains(httpMethod)));
    }

    [Theory]
    [InlineData("GET", "/api/company-portal/{companyId:guid}/interim-candidates")]
    [InlineData("GET", "/api/company-portal/{companyId:guid}/interim-settings")]
    [InlineData("PUT", "/api/company-portal/{companyId:guid}/interim-settings")]
    [InlineData("POST", "/api/company-portal/{companyId:guid}/interim-candidates/{requestId:guid}/approve")]
    [InlineData("POST", "/api/company-portal/{companyId:guid}/interim-candidates/{requestId:guid}/reject")]
    public void CompanyPortalInterimRoutes_AreMapped(string httpMethod, string routePattern)
    {
        var endpoints = BuildCompanyPortalEndpoints();

        Assert.Contains(endpoints, endpoint =>
            endpoint.RoutePattern.RawText == routePattern
            && endpoint.Metadata
                .OfType<HttpMethodMetadata>()
                .Any(metadata => metadata.HttpMethods.Contains(httpMethod)));
    }

    [Theory]
    [InlineData("GET", "/api/company-portal/{companyId:guid}/profile")]
    [InlineData("PUT", "/api/company-portal/{companyId:guid}/profile/company")]
    [InlineData("PUT", "/api/company-portal/{companyId:guid}/profile/contact")]
    [InlineData("PUT", "/api/company-portal/{companyId:guid}/profile/operations")]
    [InlineData("PUT", "/api/company-portal/{companyId:guid}/profile/payment")]
    [InlineData("POST", "/api/company-portal/{companyId:guid}/compliance-documents")]
    public void CompanyPortalProfileRoutes_AreMapped(string httpMethod, string routePattern)
    {
        var endpoints = BuildCompanyPortalEndpoints();

        Assert.Contains(endpoints, endpoint =>
            endpoint.RoutePattern.RawText == routePattern
            && endpoint.Metadata
                .OfType<HttpMethodMetadata>()
                .Any(metadata => metadata.HttpMethods.Contains(httpMethod)));
    }

    [Theory]
    [InlineData("GET", "/api/company-portal/{companyId:guid}/missions")]
    [InlineData("GET", "/api/company-portal/{companyId:guid}/missions/{missionId:guid}/assignable-providers")]
    [InlineData("GET", "/api/company-portal/{companyId:guid}/mission-offers")]
    [InlineData("POST", "/api/company-portal/{companyId:guid}/mission-offers/{offerId:guid}/accept")]
    [InlineData("POST", "/api/company-portal/{companyId:guid}/missions/{missionId:guid}/assign")]
    [InlineData("POST", "/api/company-portal/{companyId:guid}/missions/{missionId:guid}/additional-quotes/{quoteId:guid}/submit")]
    [InlineData("POST", "/api/company-portal/{companyId:guid}/missions/{missionId:guid}/cancel")]
    public void CompanyPortalMissionRoutes_AreMapped(string httpMethod, string routePattern)
    {
        var endpoints = BuildCompanyPortalEndpoints();

        Assert.Contains(endpoints, endpoint =>
            endpoint.RoutePattern.RawText == routePattern
            && endpoint.Metadata
                .OfType<HttpMethodMetadata>()
                .Any(metadata => metadata.HttpMethods.Contains(httpMethod)));
    }

    [Theory]
    [InlineData("GET", "/api/company-portal/{companyId:guid}/employees")]
    [InlineData("POST", "/api/company-portal/{companyId:guid}/employees")]
    [InlineData("PUT", "/api/company-portal/{companyId:guid}/employees/{employeeId:guid}")]
    [InlineData("PUT", "/api/company-portal/{companyId:guid}/employees/{employeeId:guid}/services")]
    [InlineData("POST", "/api/company-portal/{companyId:guid}/employees/{employeeId:guid}/availability")]
    [InlineData("POST", "/api/company-portal/{companyId:guid}/employees/{employeeId:guid}/suspend")]
    [InlineData("POST", "/api/company-portal/{companyId:guid}/employees/{employeeId:guid}/approve")]
    [InlineData("DELETE", "/api/company-portal/{companyId:guid}/employees/{employeeId:guid}")]
    [InlineData("POST", "/api/company-portal/{companyId:guid}/employees/{employeeId:guid}/invitation-code")]
    [InlineData("POST", "/api/company-portal/{companyId:guid}/employees/{employeeId:guid}/documents")]
    [InlineData("DELETE", "/api/company-portal/{companyId:guid}/employees/{employeeId:guid}/documents/{documentId:guid}")]
    [InlineData("GET", "/api/company-portal/provider-documents/{id:guid}/preview")]
    public void CompanyPortalEmployeeRoutes_AreMapped(string httpMethod, string routePattern)
    {
        var endpoints = BuildCompanyPortalEndpoints();

        Assert.Contains(endpoints, endpoint =>
            endpoint.RoutePattern.RawText == routePattern
            && endpoint.Metadata
                .OfType<HttpMethodMetadata>()
                .Any(metadata => metadata.HttpMethods.Contains(httpMethod)));
    }

    [Theory]
    [InlineData("GET", "/api/company-portal/{companyId:guid}/payments")]
    public void CompanyPortalPaymentRoutes_AreMapped(string httpMethod, string routePattern)
    {
        var endpoints = BuildCompanyPortalEndpoints();

        Assert.Contains(endpoints, endpoint =>
            endpoint.RoutePattern.RawText == routePattern
            && endpoint.Metadata
                .OfType<HttpMethodMetadata>()
                .Any(metadata => metadata.HttpMethods.Contains(httpMethod)));
    }

    private static IReadOnlyList<RouteEndpoint> BuildCompanyPortalEndpoints()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddApplicationServices();
        builder.Services.AddApiStorageServices();
        builder.Services.AddDbContext<HomeServiceDbContext>(options =>
            options.UseInMemoryDatabase($"company-portal-endpoint-contract-{Guid.NewGuid():N}"));
        builder.Services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<HomeServiceDbContext>());

        var app = builder.Build();
        app.MapCompanyPortalEndpoints();

        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();
    }
}
