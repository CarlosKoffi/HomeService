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
    [InlineData("POST", "/api/admin/auth/login")]
    [InlineData("GET", "/api/admin/auth/me")]
    [InlineData("POST", "/api/admin/auth/logout")]
    [InlineData("GET", "/api/admin/dashboard")]
    [InlineData("GET", "/api/admin/access-control")]
    [InlineData("POST", "/api/admin/access-control/admins/invitations")]
    [InlineData("GET", "/api/admin/access-control/admins/invitations/{token}")]
    [InlineData("POST", "/api/admin/access-control/admins/invitations/{token}/password")]
    [InlineData("GET", "/api/admin/audit-logs")]
    public void AdminCoreRoutes_AreMapped(string httpMethod, string routePattern)
    {
        var endpoints = BuildAdminEndpoints();

        Assert.Contains(endpoints, endpoint =>
            endpoint.RoutePattern.RawText == routePattern
            && endpoint.Metadata
                .OfType<HttpMethodMetadata>()
                .Any(metadata => metadata.HttpMethods.Contains(httpMethod)));
    }

    [Theory]
    [InlineData("GET", "/api/admin/contact-requests")]
    [InlineData("POST", "/api/admin/contact-requests/{id:guid}/in-progress")]
    [InlineData("POST", "/api/admin/contact-requests/{id:guid}/close")]
    public void AdminContactRoutes_AreMapped(string httpMethod, string routePattern)
    {
        var endpoints = BuildAdminEndpoints();

        Assert.Contains(endpoints, endpoint =>
            endpoint.RoutePattern.RawText == routePattern
            && endpoint.Metadata
                .OfType<HttpMethodMetadata>()
                .Any(metadata => metadata.HttpMethods.Contains(httpMethod)));
    }

    [Theory]
    [InlineData("GET", "/api/admin/cms/sites")]
    [InlineData("GET", "/api/admin/cms/sites/{id:guid}")]
    [InlineData("GET", "/api/admin/cms/sites/{siteId:guid}/pages")]
    [InlineData("GET", "/api/admin/cms/pages/{pageId:guid}")]
    [InlineData("PUT", "/api/admin/cms/content-values/{id:guid}")]
    [InlineData("POST", "/api/admin/cms/media")]
    [InlineData("POST", "/api/admin/cms/content-values/{id:guid}/media")]
    [InlineData("GET", "/api/admin/cms/component-definitions")]
    public void AdminCmsRoutes_AreMapped(string httpMethod, string routePattern)
    {
        var endpoints = BuildAdminEndpoints();

        Assert.Contains(endpoints, endpoint =>
            endpoint.RoutePattern.RawText == routePattern
            && endpoint.Metadata
                .OfType<HttpMethodMetadata>()
                .Any(metadata => metadata.HttpMethods.Contains(httpMethod)));
    }

    [Theory]
    [InlineData("GET", "/api/admin/translations")]
    [InlineData("POST", "/api/admin/translations")]
    [InlineData("GET", "/api/admin/country-brandings/{countryCode}")]
    [InlineData("PUT", "/api/admin/country-brandings/{countryCode}")]
    public void AdminContentConfigurationRoutes_AreMapped(string httpMethod, string routePattern)
    {
        var endpoints = BuildAdminEndpoints();

        Assert.Contains(endpoints, endpoint =>
            endpoint.RoutePattern.RawText == routePattern
            && endpoint.Metadata
                .OfType<HttpMethodMetadata>()
                .Any(metadata => metadata.HttpMethods.Contains(httpMethod)));
    }

    [Theory]
    [InlineData("GET", "/api/admin/notification-templates")]
    [InlineData("POST", "/api/admin/notification-templates")]
    [InlineData("PUT", "/api/admin/notification-templates/{id:guid}")]
    [InlineData("GET", "/api/admin/notification-delivery-rules")]
    [InlineData("PUT", "/api/admin/notification-delivery-rules/{id:guid}")]
    [InlineData("GET", "/api/admin/notifications")]
    [InlineData("GET", "/api/admin/company-portal-notifications")]
    [InlineData("POST", "/api/admin/notifications/{id:guid}/retry")]
    [InlineData("POST", "/api/admin/notifications/{id:guid}/cancel")]
    [InlineData("POST", "/api/admin/notifications/{id:guid}/mark-sent")]
    [InlineData("POST", "/api/admin/companies/{companyId:guid}/notifications/{notificationId:guid}/mark-read")]
    [InlineData("POST", "/api/admin/companies/{companyId:guid}/notifications/{notificationId:guid}/mark-unread")]
    [InlineData("POST", "/api/admin/companies/{companyId:guid}/notifications/{notificationId:guid}/resend")]
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

    [Theory]
    [InlineData("GET", "/api/admin/mission-settings")]
    [InlineData("PUT", "/api/admin/mission-settings/commission-rules/{ruleId:guid}")]
    [InlineData("PUT", "/api/admin/mission-settings/workflow-settings/{settingId:guid}")]
    public void AdminMissionSettingsRoutes_AreMapped(string httpMethod, string routePattern)
    {
        var endpoints = BuildAdminEndpoints();

        Assert.Contains(endpoints, endpoint =>
            endpoint.RoutePattern.RawText == routePattern
            && endpoint.Metadata
                .OfType<HttpMethodMetadata>()
                .Any(metadata => metadata.HttpMethods.Contains(httpMethod)));
    }

    [Theory]
    [InlineData("GET", "/api/admin/payments")]
    public void AdminPaymentRoutes_AreMapped(string httpMethod, string routePattern)
    {
        var endpoints = BuildAdminEndpoints();

        Assert.Contains(endpoints, endpoint =>
            endpoint.RoutePattern.RawText == routePattern
            && endpoint.Metadata
                .OfType<HttpMethodMetadata>()
                .Any(metadata => metadata.HttpMethods.Contains(httpMethod)));
    }

    [Theory]
    [InlineData("GET", "/api/admin/companies")]
    [InlineData("GET", "/api/admin/companies/{companyId:guid}")]
    [InlineData("PUT", "/api/admin/companies/{id:guid}/assignment-mode")]
    [InlineData("PUT", "/api/admin/companies/{id:guid}/dispatch-settings")]
    [InlineData("POST", "/api/admin/companies/{id:guid}/suspend")]
    [InlineData("POST", "/api/admin/companies/{id:guid}/reactivate")]
    public void AdminCompanyRoutes_AreMapped(string httpMethod, string routePattern)
    {
        var endpoints = BuildAdminEndpoints();

        Assert.Contains(endpoints, endpoint =>
            endpoint.RoutePattern.RawText == routePattern
            && endpoint.Metadata
                .OfType<HttpMethodMetadata>()
                .Any(metadata => metadata.HttpMethods.Contains(httpMethod)));
    }

    [Theory]
    [InlineData("GET", "/api/admin/providers")]
    [InlineData("GET", "/api/admin/providers/{providerId:guid}")]
    [InlineData("POST", "/api/admin/providers/{providerId:guid}/approve")]
    [InlineData("POST", "/api/admin/providers/{providerId:guid}/suspend")]
    [InlineData("GET", "/api/admin/provider-documents/{id:guid}/preview")]
    public void AdminProviderRoutes_AreMapped(string httpMethod, string routePattern)
    {
        var endpoints = BuildAdminEndpoints();

        Assert.Contains(endpoints, endpoint =>
            endpoint.RoutePattern.RawText == routePattern
            && endpoint.Metadata
                .OfType<HttpMethodMetadata>()
                .Any(metadata => metadata.HttpMethods.Contains(httpMethod)));
    }

    [Theory]
    [InlineData("GET", "/api/admin/company-service-proposals")]
    [InlineData("GET", "/api/admin/service-insights")]
    [InlineData("POST", "/api/admin/company-service-proposals/reanalyse")]
    [InlineData("POST", "/api/admin/company-service-proposals/{id:guid}/attach")]
    [InlineData("POST", "/api/admin/company-service-proposals/{id:guid}/create-prestation")]
    [InlineData("POST", "/api/admin/company-service-proposals/{id:guid}/create-service")]
    [InlineData("POST", "/api/admin/services")]
    [InlineData("PUT", "/api/admin/services/{serviceId:guid}")]
    [InlineData("POST", "/api/admin/services/{serviceId:guid}/activate")]
    [InlineData("POST", "/api/admin/services/{serviceId:guid}/deactivate")]
    [InlineData("POST", "/api/admin/services/{serviceId:guid}/prestations")]
    [InlineData("PUT", "/api/admin/service-prestations/{id:guid}")]
    [InlineData("POST", "/api/admin/service-prestations/{id:guid}/activate")]
    [InlineData("POST", "/api/admin/service-prestations/{id:guid}/deactivate")]
    public void AdminServiceCatalogRoutes_AreMapped(string httpMethod, string routePattern)
    {
        var endpoints = BuildAdminEndpoints();

        Assert.Contains(endpoints, endpoint =>
            endpoint.RoutePattern.RawText == routePattern
            && endpoint.Metadata
                .OfType<HttpMethodMetadata>()
                .Any(metadata => metadata.HttpMethods.Contains(httpMethod)));
    }

    [Theory]
    [InlineData("GET", "/api/admin/company-applications")]
    [InlineData("GET", "/api/admin/company-applications/{id:guid}")]
    [InlineData("POST", "/api/admin/company-applications/{id:guid}/approve")]
    [InlineData("POST", "/api/admin/company-applications/{id:guid}/reject")]
    [InlineData("POST", "/api/admin/company-applications/{id:guid}/reopen")]
    [InlineData("POST", "/api/admin/company-applications/{id:guid}/request-more-information")]
    [InlineData("POST", "/api/admin/company-applications/{id:guid}/activation-link")]
    [InlineData("POST", "/api/admin/company-application-documents/{id:guid}/approve")]
    [InlineData("POST", "/api/admin/company-application-documents/{id:guid}/reject")]
    [InlineData("POST", "/api/admin/company-application-documents/{id:guid}/request-replacement")]
    [InlineData("POST", "/api/admin/company-application-documents/{id:guid}/reopen")]
    [InlineData("GET", "/api/admin/company-application-documents/{id:guid}/preview")]
    [InlineData("GET", "/api/admin/company-application-documents/{id:guid}/download")]
    public void AdminCompanyApplicationRoutes_AreMapped(string httpMethod, string routePattern)
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
