using HomeService.Api;
using HomeService.Api.Endpoints;
using HomeService.Application;
using HomeService.Application.Auditing;
using HomeService.Contracts.Admin;
using HomeService.Domain.Enums;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Text.RegularExpressions;

namespace HomeService.Tests.Integration;

public sealed class AdminEndpointContractTests
{
    [Theory]
    [InlineData("POST", "/api/admin/auth/login")]
    [InlineData("GET", "/api/admin/auth/me")]
    [InlineData("POST", "/api/admin/auth/logout")]
    [InlineData("GET", "/api/admin/dashboard")]
    [InlineData("GET", "/api/admin/access-control")]
    [InlineData("POST", "/api/admin/access-control/roles")]
    [InlineData("PUT", "/api/admin/access-control/roles/{roleId:guid}/permissions")]
    [InlineData("POST", "/api/admin/access-control/admins")]
    [InlineData("POST", "/api/admin/access-control/admins/invitations")]
    [InlineData("GET", "/api/admin/access-control/admins/invitations/{token}")]
    [InlineData("POST", "/api/admin/access-control/admins/invitations/{token}/password")]
    [InlineData("PUT", "/api/admin/access-control/admins/{adminUserId:guid}/profile")]
    [InlineData("POST", "/api/admin/access-control/admins/{adminUserId:guid}/invitation")]
    [InlineData("PUT", "/api/admin/access-control/admins/{adminUserId:guid}/roles")]
    [InlineData("POST", "/api/admin/access-control/admins/{adminUserId:guid}/deactivate")]
    [InlineData("POST", "/api/admin/access-control/admins/{adminUserId:guid}/reactivate")]
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

    [Fact]
    public void AdminClientRoutes_AreBackedByMappedApiEndpoints()
    {
        var endpointPatterns = BuildAdminEndpoints()
            .Select(endpoint => NormalizeAdminRoute(endpoint.RoutePattern.RawText ?? string.Empty))
            .Where(pattern => pattern.StartsWith("/api/admin", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

        var clientRoutes = ExtractAdminClientRoutes()
            .Select(NormalizeAdminRoute)
            .Where(route => route.StartsWith("/api/admin", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var missingRoutes = clientRoutes
            .Where(route => !endpointPatterns.Contains(route))
            .ToList();

        Assert.True(
            missingRoutes.Count == 0,
            "Admin client calls without mapped API endpoint:" + Environment.NewLine + string.Join(Environment.NewLine, missingRoutes));
    }

    [Theory]
    [InlineData("POST", "/api/admin/companies/11111111-1111-1111-1111-111111111111/notifications/22222222-2222-2222-2222-222222222222/mark-read", AdminModuleKey.Notifications, AdminPermissionAction.Edit)]
    [InlineData("POST", "/api/admin/companies/11111111-1111-1111-1111-111111111111/notifications/22222222-2222-2222-2222-222222222222/mark-unread", AdminModuleKey.Notifications, AdminPermissionAction.Edit)]
    [InlineData("POST", "/api/admin/companies/11111111-1111-1111-1111-111111111111/notifications/22222222-2222-2222-2222-222222222222/resend", AdminModuleKey.Notifications, AdminPermissionAction.Resend)]
    [InlineData("POST", "/api/admin/notifications/11111111-1111-1111-1111-111111111111/retry", AdminModuleKey.Notifications, AdminPermissionAction.Resend)]
    [InlineData("POST", "/api/admin/notifications/11111111-1111-1111-1111-111111111111/cancel", AdminModuleKey.Notifications, AdminPermissionAction.Reject)]
    [InlineData("POST", "/api/admin/notifications/11111111-1111-1111-1111-111111111111/mark-sent", AdminModuleKey.Notifications, AdminPermissionAction.Approve)]
    [InlineData("POST", "/api/admin/notification-templates", AdminModuleKey.Notifications, AdminPermissionAction.Create)]
    [InlineData("PUT", "/api/admin/notification-templates/11111111-1111-1111-1111-111111111111", AdminModuleKey.Notifications, AdminPermissionAction.Edit)]
    [InlineData("PUT", "/api/admin/notification-delivery-rules/11111111-1111-1111-1111-111111111111", AdminModuleKey.Notifications, AdminPermissionAction.Edit)]
    [InlineData("POST", "/api/admin/companies/11111111-1111-1111-1111-111111111111/suspend", AdminModuleKey.CompanyManagement, AdminPermissionAction.Suspend)]
    [InlineData("POST", "/api/admin/missions/11111111-1111-1111-1111-111111111111/dispatch-offers", AdminModuleKey.Missions, AdminPermissionAction.Resend)]
    [InlineData("POST", "/api/admin/missions/11111111-1111-1111-1111-111111111111/mark-disputed", AdminModuleKey.Missions, AdminPermissionAction.Edit)]
    [InlineData("POST", "/api/admin/missions/11111111-1111-1111-1111-111111111111/resolve-dispute", AdminModuleKey.Missions, AdminPermissionAction.Approve)]
    [InlineData("POST", "/api/admin/missions/11111111-1111-1111-1111-111111111111/cancel", AdminModuleKey.Missions, AdminPermissionAction.Reject)]
    [InlineData("POST", "/api/admin/providers/11111111-1111-1111-1111-111111111111/approve", AdminModuleKey.ProviderReview, AdminPermissionAction.Approve)]
    [InlineData("POST", "/api/admin/providers/11111111-1111-1111-1111-111111111111/suspend", AdminModuleKey.ProviderReview, AdminPermissionAction.Suspend)]
    [InlineData("POST", "/api/admin/company-applications/11111111-1111-1111-1111-111111111111/request-more-information", AdminModuleKey.CompanyApplications, AdminPermissionAction.Edit)]
    [InlineData("POST", "/api/admin/company-applications/11111111-1111-1111-1111-111111111111/reopen", AdminModuleKey.CompanyApplications, AdminPermissionAction.Edit)]
    [InlineData("POST", "/api/admin/company-application-documents/11111111-1111-1111-1111-111111111111/request-replacement", AdminModuleKey.CompanyApplications, AdminPermissionAction.Edit)]
    [InlineData("POST", "/api/admin/companies/11111111-1111-1111-1111-111111111111/reactivate", AdminModuleKey.CompanyManagement, AdminPermissionAction.Edit)]
    [InlineData("POST", "/api/admin/contact-requests/11111111-1111-1111-1111-111111111111/in-progress", AdminModuleKey.ContactRequests, AdminPermissionAction.Edit)]
    [InlineData("POST", "/api/admin/contact-requests/11111111-1111-1111-1111-111111111111/close", AdminModuleKey.ContactRequests, AdminPermissionAction.Edit)]
    [InlineData("POST", "/api/admin/company-service-proposals/11111111-1111-1111-1111-111111111111/attach", AdminModuleKey.Services, AdminPermissionAction.Edit)]
    [InlineData("POST", "/api/admin/company-service-proposals/11111111-1111-1111-1111-111111111111/create-prestation", AdminModuleKey.Services, AdminPermissionAction.Create)]
    [InlineData("POST", "/api/admin/company-service-proposals/11111111-1111-1111-1111-111111111111/create-service", AdminModuleKey.Services, AdminPermissionAction.Create)]
    [InlineData("POST", "/api/admin/company-service-proposals/reanalyse", AdminModuleKey.Services, AdminPermissionAction.Edit)]
    [InlineData("POST", "/api/admin/services", AdminModuleKey.Services, AdminPermissionAction.Create)]
    [InlineData("PUT", "/api/admin/services/11111111-1111-1111-1111-111111111111", AdminModuleKey.Services, AdminPermissionAction.Edit)]
    [InlineData("POST", "/api/admin/services/11111111-1111-1111-1111-111111111111/activate", AdminModuleKey.Services, AdminPermissionAction.Edit)]
    [InlineData("POST", "/api/admin/services/11111111-1111-1111-1111-111111111111/deactivate", AdminModuleKey.Services, AdminPermissionAction.Suspend)]
    [InlineData("POST", "/api/admin/services/11111111-1111-1111-1111-111111111111/prestations", AdminModuleKey.Services, AdminPermissionAction.Create)]
    [InlineData("PUT", "/api/admin/service-prestations/11111111-1111-1111-1111-111111111111", AdminModuleKey.Services, AdminPermissionAction.Edit)]
    [InlineData("POST", "/api/admin/service-prestations/11111111-1111-1111-1111-111111111111/activate", AdminModuleKey.Services, AdminPermissionAction.Edit)]
    [InlineData("POST", "/api/admin/service-prestations/11111111-1111-1111-1111-111111111111/deactivate", AdminModuleKey.Services, AdminPermissionAction.Suspend)]
    [InlineData("POST", "/api/admin/access-control/admins/invitations", AdminModuleKey.AdminAccess, AdminPermissionAction.ManageRoles)]
    [InlineData("PUT", "/api/admin/access-control/admins/11111111-1111-1111-1111-111111111111/profile", AdminModuleKey.AdminAccess, AdminPermissionAction.ManageRoles)]
    [InlineData("POST", "/api/admin/access-control/admins/11111111-1111-1111-1111-111111111111/invitation", AdminModuleKey.AdminAccess, AdminPermissionAction.ManageRoles)]
    [InlineData("POST", "/api/admin/access-control/admins/11111111-1111-1111-1111-111111111111/deactivate", AdminModuleKey.AdminAccess, AdminPermissionAction.ManageRoles)]
    [InlineData("POST", "/api/admin/access-control/admins/11111111-1111-1111-1111-111111111111/reactivate", AdminModuleKey.AdminAccess, AdminPermissionAction.ManageRoles)]
    [InlineData("GET", "/api/admin/country-brandings/CI", AdminModuleKey.Localization, AdminPermissionAction.View)]
    [InlineData("PUT", "/api/admin/country-brandings/CI", AdminModuleKey.Localization, AdminPermissionAction.Edit)]
    public void AdminPermissionResolver_MapsSensitiveActionsToExpectedModuleAndPermission(
        string httpMethod,
        string path,
        AdminModuleKey expectedModule,
        AdminPermissionAction expectedAction)
    {
        var permission = AdminEndpointPermissionResolver.Resolve(httpMethod, path);

        Assert.Equal(expectedModule, permission.ModuleKey);
        Assert.Equal(expectedAction, permission.Action);
    }

    [Theory]
    [InlineData("/api/admin/auth/login", true)]
    [InlineData("/api/admin/access-control/admins/invitations", false)]
    [InlineData("/api/admin/access-control/admins/invitations/token-value", true)]
    [InlineData("/api/admin/access-control/admins/invitations/token-value/password", true)]
    public void AdminSessionBypass_OnlyAllowsAuthAndInvitationTokenRoutes(string path, bool expectedSkip)
    {
        var method = typeof(AdminEndpoints).GetMethod("ShouldSkipAdminSessionCheck", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var shouldSkip = (bool)method!.Invoke(null, [new PathString(path)])!;

        Assert.Equal(expectedSkip, shouldSkip);
    }

    [Fact]
    public void AdminAuditActor_UsesConnectedAdminFullName()
    {
        var method = typeof(AdminEndpoints).GetMethod("GetAdminAuditActor", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var httpContext = new DefaultHttpContext();
        var adminId = Guid.NewGuid();
        httpContext.Items["CurrentAdminUser"] = new AdminCurrentUserResponse(
            adminId,
            "Awa Kone",
            "awa.kone@wele.ci",
            true,
            DateTimeOffset.UtcNow.AddHours(1),
            []);

        var actor = (AuditActor)method!.Invoke(null, [httpContext.Request])!;

        Assert.Equal(AuditActorType.Admin, actor.Type);
        Assert.Equal(adminId, actor.Id);
        Assert.Equal("Awa Kone", actor.DisplayName);
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

    private static IEnumerable<string> ExtractAdminClientRoutes()
    {
        var repositoryRoot = FindRepositoryRoot();
        var clientPath = Path.Combine(repositoryRoot.FullName, "src", "HomeService.Admin", "Services", "PlatformApiClient.cs");
        var content = File.ReadAllText(clientPath);

        foreach (Match match in Regex.Matches(content, "\"(?<route>/api/admin[^\"]+)\"", RegexOptions.CultureInvariant))
        {
            yield return match.Groups["route"].Value;
        }
    }

    private static string NormalizeAdminRoute(string route)
    {
        var queryIndex = route.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex >= 0)
        {
            route = route[..queryIndex];
        }

        route = route.Replace("{suffix}", string.Empty, StringComparison.Ordinal);
        route = Regex.Replace(route, "\\{[^}/]+\\}", "{}", RegexOptions.CultureInvariant);
        route = Regex.Replace(route, "/+", "/", RegexOptions.CultureInvariant);

        return route.TrimEnd('/');
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "HomeService.Admin"))
                && Directory.Exists(Path.Combine(directory.FullName, "tests", "HomeService.Tests.Integration")))
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found from integration test output directory.");
    }
}
