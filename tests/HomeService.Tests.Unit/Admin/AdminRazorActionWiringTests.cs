using System.Text.RegularExpressions;

namespace HomeService.Tests.Unit.Admin;

public sealed class AdminRazorActionWiringTests
{
    [Fact]
    public void AdminPages_DoNotExposeButtonsWithoutActions()
    {
        var pagesDirectory = FindRepositoryRoot()
            .GetDirectories("src", SearchOption.TopDirectoryOnly)
            .Single()
            .GetDirectories("HomeService.Admin", SearchOption.TopDirectoryOnly)
            .Single()
            .GetDirectories("Components", SearchOption.TopDirectoryOnly)
            .Single()
            .GetDirectories("Pages", SearchOption.TopDirectoryOnly)
            .Single();

        var missingActions = pagesDirectory
            .EnumerateFiles("*.razor", SearchOption.TopDirectoryOnly)
            .SelectMany(FindButtonsWithoutAction)
            .ToList();

        Assert.True(
            missingActions.Count == 0,
            "Admin buttons without @onclick or submit behavior:" + Environment.NewLine + string.Join(Environment.NewLine, missingActions));
    }

    [Fact]
    public void AdminNavigationLinks_TargetExistingPages()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pagesDirectory = GetAdminPagesDirectory(repositoryRoot);

        var pageRoutes = pagesDirectory
            .EnumerateFiles("*.razor", SearchOption.TopDirectoryOnly)
            .SelectMany(ReadPageRoutes)
            .Select(NormalizeRoute)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var navMenu = File.ReadAllText(Path.Combine(
            repositoryRoot.FullName,
            "src",
            "HomeService.Admin",
            "Components",
            "Layout",
            "NavMenu.razor"));
        var missingRoutes = Regex.Matches(navMenu, "href\\s*=\\s*\"(?<href>[^\"]*)\"", RegexOptions.IgnoreCase)
            .Select(match => match.Groups["href"].Value)
            .Where(href => !href.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            .Select(NormalizeRoute)
            .Where(route => !pageRoutes.Contains(route))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.True(
            missingRoutes.Count == 0,
            "Admin navigation links without matching page route:" + Environment.NewLine + string.Join(Environment.NewLine, missingRoutes));
    }

    [Fact]
    public void AdminPageInternalLinks_TargetExistingPages()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pagesDirectory = GetAdminPagesDirectory(repositoryRoot);
        var pageRoutePatterns = pagesDirectory
            .EnumerateFiles("*.razor", SearchOption.TopDirectoryOnly)
            .SelectMany(ReadPageRoutes)
            .Select(NormalizeRoute)
            .Select(ToRouteRegex)
            .ToList();

        var brokenLinks = pagesDirectory
            .EnumerateFiles("*.razor", SearchOption.TopDirectoryOnly)
            .SelectMany(file => ReadInternalStaticLinks(file)
                .Where(link => !MatchesAnyRoute(link.Href, pageRoutePatterns))
                .Select(link => $"{file.Name}:{link.LineNumber} -> {link.Href}"))
            .ToList();

        Assert.True(
            brokenLinks.Count == 0,
            "Admin page links without matching page route:" + Environment.NewLine + string.Join(Environment.NewLine, brokenLinks));
    }

    [Fact]
    public void AdminActionPages_CallExpectedApiActions()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pagesDirectory = GetAdminPagesDirectory(repositoryRoot);
        var expectedActionsByPage = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["AccessControl.razor"] =
            [
                "CreateAdminRoleAsync",
                "UpdateAdminRolePermissionsAsync",
                "CreateAdminInvitationAsync",
                "UpdateAdminUserProfileAsync",
                "RegenerateAdminInvitationAsync",
                "UpdateAdminUserRolesAsync",
                "DeactivateAdminUserAsync",
                "ReactivateAdminUserAsync"
            ],
            ["AdminMissionDetail.razor"] =
            [
                "CreateAdminMissionDispatchOffersAsync",
                "MarkAdminMissionDisputedAsync",
                "ResolveAdminMissionDisputeAsync",
                "CancelAdminMissionAsync"
            ],
            ["AdminMissions.razor"] =
            [
                "CreateAdminMissionDispatchOffersAsync",
                "MarkAdminMissionDisputedAsync",
                "CancelAdminMissionAsync"
            ],
            ["AdminProviderDetail.razor"] =
            [
                "ApproveAdminProviderAsync",
                "SuspendAdminProviderAsync"
            ],
            ["CompanyApplicationDetail.razor"] =
            [
                "ApproveCompanyApplicationAsync",
                "RejectCompanyApplicationAsync",
                "ReopenCompanyApplicationAsync",
                "RequestCompanyApplicationMoreInformationAsync",
                "SendCompanyApplicationActivationLinkAsync",
                "ApproveCompanyApplicationDocumentAsync",
                "RejectCompanyApplicationDocumentAsync",
                "RequestCompanyApplicationDocumentReplacementAsync",
                "ReopenCompanyApplicationDocumentAsync"
            ],
            ["CompanyDetail.razor"] =
            [
                "SuspendAdminCompanyAsync",
                "ReactivateAdminCompanyAsync",
                "UpdateCompanyDispatchSettingsAsync",
                "MarkCompanyNotificationReadAsync",
                "MarkCompanyNotificationUnreadAsync",
                "ResendCompanyNotificationAsync"
            ],
            ["Cms.razor"] =
            [
                "GetCmsSitesAsync",
                "GetCmsSiteAsync",
                "GetCmsPageAsync",
                "GetCmsComponentDefinitionsAsync",
                "UpdateCmsContentValueAsync",
                "UploadCmsMediaAsync"
            ],
            ["ContactRequests.razor"] =
            [
                "MarkContactRequestInProgressAsync",
                "CloseContactRequestAsync"
            ],
            ["Localization.razor"] =
            [
                "UpsertAdminTranslationAsync"
            ],
            ["MissionSettings.razor"] =
            [
                "UpdateAdminCommissionRuleAsync",
                "UpdateAdminMissionWorkflowSettingAsync"
            ],
            ["Notifications.razor"] =
            [
                "RetryNotificationAsync",
                "CancelNotificationAsync",
                "MarkNotificationSentAsync",
                "MarkCompanyNotificationReadAsync",
                "MarkCompanyNotificationUnreadAsync",
                "ResendCompanyNotificationAsync",
                "UpdateNotificationDeliveryRuleAsync",
                "CreateNotificationTemplateAsync",
                "UpdateNotificationTemplateAsync"
            ],
            ["ServiceProposals.razor"] =
            [
                "AttachCompanyServiceProposalAsync",
                "ReanalyseCompanyServiceProposalsAsync",
                "CreatePrestationFromCompanyServiceProposalAsync",
                "CreateServiceFromCompanyServiceProposalAsync",
                "CreateServiceAsync",
                "UpdateServiceAsync",
                "ActivateServiceAsync",
                "DeactivateServiceAsync",
                "CreateServicePrestationAsync",
                "UpdateServicePrestationAsync",
                "ActivateServicePrestationAsync",
                "DeactivateServicePrestationAsync"
            ]
        };

        var missingActions = expectedActionsByPage
            .SelectMany(entry => FindMissingApiActions(pagesDirectory, entry.Key, entry.Value))
            .ToList();

        Assert.True(
            missingActions.Count == 0,
            "Admin action pages missing expected API calls:" + Environment.NewLine + string.Join(Environment.NewLine, missingActions));
    }

    private static IEnumerable<string> FindButtonsWithoutAction(FileInfo file)
    {
        var content = File.ReadAllText(file.FullName);
        var lineStarts = GetLineStarts(content);

        foreach (Match match in Regex.Matches(content, "<button\\b(?<attributes>[^>]*)>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            var attributes = match.Groups["attributes"].Value;
            if (attributes.Contains("@onclick", StringComparison.OrdinalIgnoreCase)
                || Regex.IsMatch(attributes, "type\\s*=\\s*[\"']submit[\"']", RegexOptions.IgnoreCase))
            {
                continue;
            }

            var lineNumber = GetLineNumber(lineStarts, match.Index);
            yield return $"{file.Name}:{lineNumber}";
        }
    }

    private static IEnumerable<string> ReadPageRoutes(FileInfo file)
    {
        var content = File.ReadAllText(file.FullName);
        return Regex.Matches(content, "^@page\\s+\"(?<route>[^\"]+)\"", RegexOptions.Multiline)
            .Select(match => match.Groups["route"].Value);
    }

    private static IEnumerable<AdminPageLink> ReadInternalStaticLinks(FileInfo file)
    {
        var content = File.ReadAllText(file.FullName);
        var lineStarts = GetLineStarts(content);
        foreach (Match match in Regex.Matches(content, "href\\s*=\\s*\"(?<href>[^\"]+)\"", RegexOptions.IgnoreCase))
        {
            var href = match.Groups["href"].Value.Trim();
            if (ShouldIgnoreHref(href))
            {
                continue;
            }

            yield return new AdminPageLink(NormalizeRoute(href), GetLineNumber(lineStarts, match.Index));
        }
    }

    private static bool ShouldIgnoreHref(string href)
    {
        return string.IsNullOrWhiteSpace(href)
            || href.StartsWith('@')
            || href.StartsWith('#')
            || href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            || href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
            || href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRoute(string route)
    {
        var cleanRoute = route.Split('?', '#')[0].Trim();
        if (cleanRoute == "/" || cleanRoute.Length == 0)
        {
            return string.Empty;
        }

        return cleanRoute.Trim('/');
    }

    private static Regex ToRouteRegex(string route)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return new Regex("^$", RegexOptions.IgnoreCase);
        }

        var segments = route.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.StartsWith('{') && segment.EndsWith('}')
                ? "[^/]+"
                : Regex.Escape(segment));
        return new Regex("^" + string.Join("/", segments) + "$", RegexOptions.IgnoreCase);
    }

    private static bool MatchesAnyRoute(string href, IReadOnlyList<Regex> routePatterns)
    {
        var normalized = Regex.Replace(href, "@[^/]+", "dynamic-value");
        return routePatterns.Any(pattern => pattern.IsMatch(normalized));
    }

    private static IEnumerable<string> FindMissingApiActions(DirectoryInfo pagesDirectory, string pageName, IReadOnlyList<string> expectedActions)
    {
        var page = new FileInfo(Path.Combine(pagesDirectory.FullName, pageName));
        if (!page.Exists)
        {
            yield return $"{pageName} -> page introuvable";
            yield break;
        }

        var content = File.ReadAllText(page.FullName);
        foreach (var expectedAction in expectedActions)
        {
            if (!content.Contains($"ApiClient.{expectedAction}", StringComparison.Ordinal))
            {
                yield return $"{pageName} -> {expectedAction}";
            }
        }
    }

    private static DirectoryInfo GetAdminPagesDirectory(DirectoryInfo repositoryRoot)
    {
        return repositoryRoot
            .GetDirectories("src", SearchOption.TopDirectoryOnly)
            .Single()
            .GetDirectories("HomeService.Admin", SearchOption.TopDirectoryOnly)
            .Single()
            .GetDirectories("Components", SearchOption.TopDirectoryOnly)
            .Single()
            .GetDirectories("Pages", SearchOption.TopDirectoryOnly)
            .Single();
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "HomeService.Admin"))
                && Directory.Exists(Path.Combine(directory.FullName, "tests", "HomeService.Tests.Unit")))
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found from test output directory.");
    }

    private static List<int> GetLineStarts(string content)
    {
        var starts = new List<int> { 0 };
        for (var index = 0; index < content.Length; index++)
        {
            if (content[index] == '\n')
            {
                starts.Add(index + 1);
            }
        }

        return starts;
    }

    private static int GetLineNumber(IReadOnlyList<int> lineStarts, int position)
    {
        var lineIndex = 0;
        for (var index = 0; index < lineStarts.Count; index++)
        {
            if (lineStarts[index] > position)
            {
                break;
            }

            lineIndex = index;
        }

        return lineIndex + 1;
    }

    private sealed record AdminPageLink(string Href, int LineNumber);
}
