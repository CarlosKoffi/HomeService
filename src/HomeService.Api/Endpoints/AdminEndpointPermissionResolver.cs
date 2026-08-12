using HomeService.Domain.Enums;

namespace HomeService.Api.Endpoints;

public static class AdminEndpointPermissionResolver
{
    public static AdminResolvedEndpointPermission Resolve(string method, string path)
    {
        var action = ResolveAction(method, path);
        var moduleKey = ResolveModuleKey(path);
        return new AdminResolvedEndpointPermission(moduleKey, action);
    }

    public static AdminPermissionAction ResolveAction(string method, string path)
    {
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method))
        {
            return AdminPermissionAction.View;
        }

        if (path.Contains("/access-control", StringComparison.OrdinalIgnoreCase))
        {
            return AdminPermissionAction.ManageRoles;
        }

        if (path.Contains("/approve", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/validate", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/resolve-dispute", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/mark-sent", StringComparison.OrdinalIgnoreCase))
        {
            return AdminPermissionAction.Approve;
        }

        if (path.Contains("/reject", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/refuse", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/cancel", StringComparison.OrdinalIgnoreCase))
        {
            return AdminPermissionAction.Reject;
        }

        if (path.Contains("/suspend", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/deactivate", StringComparison.OrdinalIgnoreCase))
        {
            return AdminPermissionAction.Suspend;
        }

        if (path.Contains("/resend", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/retry", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/dispatch-offers", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/activation-link", StringComparison.OrdinalIgnoreCase))
        {
            return AdminPermissionAction.Resend;
        }

        if (path.Contains("/mark-read", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/mark-unread", StringComparison.OrdinalIgnoreCase))
        {
            return AdminPermissionAction.Edit;
        }

        if (path.Contains("/mark-disputed", StringComparison.OrdinalIgnoreCase))
        {
            return AdminPermissionAction.Edit;
        }

        if (path.Contains("/request-more-information", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/request-replacement", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/reopen", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/reactivate", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/activate", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/in-progress", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/close", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/attach", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/reanalyse", StringComparison.OrdinalIgnoreCase))
        {
            return AdminPermissionAction.Edit;
        }

        return HttpMethods.IsPost(method) ? AdminPermissionAction.Create : AdminPermissionAction.Edit;
    }

    public static AdminModuleKey ResolveModuleKey(string path)
    {
        if (path.Contains("/company-applications", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/company-application-documents", StringComparison.OrdinalIgnoreCase))
        {
            return AdminModuleKey.CompanyApplications;
        }

        if (path.Contains("/notifications", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/notification-", StringComparison.OrdinalIgnoreCase))
        {
            return AdminModuleKey.Notifications;
        }

        if (path.Contains("/companies", StringComparison.OrdinalIgnoreCase))
        {
            return AdminModuleKey.CompanyManagement;
        }

        if (path.Contains("/clients", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/client-attachments", StringComparison.OrdinalIgnoreCase))
        {
            return AdminModuleKey.Clients;
        }

        if (path.Contains("/providers", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/provider-documents", StringComparison.OrdinalIgnoreCase))
        {
            return AdminModuleKey.ProviderReview;
        }

        if (path.Contains("/quality", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/mission-settings", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/commission-rules", StringComparison.OrdinalIgnoreCase))
        {
            return AdminModuleKey.MissionSettings;
        }

        if (path.Contains("/missions", StringComparison.OrdinalIgnoreCase))
        {
            return AdminModuleKey.Missions;
        }

        if (path.Contains("/payments", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/payment-providers", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/company-payouts", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/company-payout-destinations", StringComparison.OrdinalIgnoreCase))
        {
            return AdminModuleKey.Payments;
        }

        if (path.Contains("/localization", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/translations", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/country-brandings", StringComparison.OrdinalIgnoreCase))
        {
            return AdminModuleKey.Localization;
        }

        if (path.Contains("/cms", StringComparison.OrdinalIgnoreCase))
        {
            return AdminModuleKey.Cms;
        }

        if (path.Contains("/service", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/company-service-proposals", StringComparison.OrdinalIgnoreCase))
        {
            return AdminModuleKey.Services;
        }

        if (path.Contains("/contact-requests", StringComparison.OrdinalIgnoreCase))
        {
            return AdminModuleKey.ContactRequests;
        }

        if (path.Contains("/audit-logs", StringComparison.OrdinalIgnoreCase))
        {
            return AdminModuleKey.Audit;
        }

        if (path.Contains("/access-control", StringComparison.OrdinalIgnoreCase))
        {
            return AdminModuleKey.AdminAccess;
        }

        return AdminModuleKey.Dashboard;
    }
}

public sealed record AdminResolvedEndpointPermission(AdminModuleKey ModuleKey, AdminPermissionAction Action);
