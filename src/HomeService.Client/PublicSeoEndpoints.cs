using System.Security;
using System.Text;
using HomeService.Client.Services;

namespace HomeService.Client;

public static class PublicSeoEndpoints
{
    public static IEndpointRouteBuilder MapPublicSeoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/robots.txt", (IConfiguration configuration) =>
        {
            var siteBaseUrl = GetSiteBaseUrl(configuration);
            var content = $$"""
                User-agent: *
                Allow: /
                Disallow: /_blazor
                Disallow: /Error

                Sitemap: {{siteBaseUrl}}/sitemap.xml
                """;
            return Results.Text(content, "text/plain", Encoding.UTF8);
        }).ExcludeFromDescription();

        endpoints.MapGet("/sitemap.xml", async (
            HttpContext context,
            PublicWebsiteApiClient apiClient,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            var siteBaseUrl = GetSiteBaseUrl(configuration);
            var services = (await apiClient.GetServicesAsync(cancellationToken))
                .Where(service => service.IsActive)
                .OrderBy(service => service.Name)
                .ToList();

            var xml = new StringBuilder();
            xml.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
            xml.AppendLine("""<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9" xmlns:image="http://www.google.com/schemas/sitemap-image/1.1">""");
            AppendUrl(xml, $"{siteBaseUrl}/", "1.0", "daily",
                [new SeoImage($"{GetPublicMediaBaseUrl(configuration)}/website/client/wele-client-hero-v2.webp", "Wélé — services à domicile à Abidjan")]);
            AppendUrl(xml, $"{siteBaseUrl}/services", "0.9", "daily", []);

            foreach (var service in services)
            {
                var publicMediaBaseUrl = GetPublicMediaBaseUrl(configuration);
                var images = service.Prestations
                    .Where(prestation => prestation.IsActive && !string.IsNullOrWhiteSpace(prestation.IllustrationUrl))
                    .Select(prestation => new SeoImage(
                        ResolvePublicMediaUrl(prestation.IllustrationUrl!, siteBaseUrl, publicMediaBaseUrl),
                        $"{prestation.Name} — {service.Name} à Abidjan"))
                    .Concat(string.IsNullOrWhiteSpace(service.ImageUrl)
                        ? string.IsNullOrWhiteSpace(service.IconUrl)
                            ? []
                            : [new SeoImage(ResolvePublicMediaUrl(service.IconUrl!, siteBaseUrl, publicMediaBaseUrl), $"{service.Name} à Abidjan")]
                        : [new SeoImage(ResolvePublicMediaUrl(service.ImageUrl!, siteBaseUrl, publicMediaBaseUrl), $"{service.Name} à Abidjan")])
                    .Where(image => Uri.TryCreate(image.Url, UriKind.Absolute, out _))
                    .DistinctBy(image => image.Url, StringComparer.OrdinalIgnoreCase)
                    .Take(10)
                    .ToList();

                AppendUrl(
                    xml,
                    $"{siteBaseUrl}/services/{ServiceSeoCatalog.ToSlug(service.Name)}",
                    "0.9",
                    "weekly",
                    images);
            }

            xml.AppendLine("</urlset>");
            context.Response.Headers.CacheControl = "public, max-age=3600";
            return Results.Text(xml.ToString(), "application/xml", Encoding.UTF8);
        }).ExcludeFromDescription();

        return endpoints;
    }

    private static void AppendUrl(
        StringBuilder xml,
        string url,
        string priority,
        string changeFrequency,
        IReadOnlyList<SeoImage> images)
    {
        xml.AppendLine("  <url>");
        xml.Append("    <loc>").Append(Escape(url)).AppendLine("</loc>");
        xml.Append("    <changefreq>").Append(changeFrequency).AppendLine("</changefreq>");
        xml.Append("    <priority>").Append(priority).AppendLine("</priority>");
        foreach (var image in images)
        {
            xml.AppendLine("    <image:image>");
            xml.Append("      <image:loc>").Append(Escape(image.Url)).AppendLine("</image:loc>");
            xml.Append("      <image:title>").Append(Escape(image.Title)).AppendLine("</image:title>");
            xml.AppendLine("    </image:image>");
        }

        xml.AppendLine("  </url>");
    }

    private static string Escape(string value) => SecurityElement.Escape(value) ?? string.Empty;

    private static string GetSiteBaseUrl(IConfiguration configuration) =>
        (configuration["Site:BaseUrl"] ?? "https://wele.africa").TrimEnd('/');

    private static string GetPublicMediaBaseUrl(IConfiguration configuration) =>
        (configuration["R2_PUBLIC_BASE_URL"]
         ?? configuration["Media:PublicBaseUrl"]
         ?? "https://media.wele.africa").TrimEnd('/');

    private static string ResolvePublicMediaUrl(string value, string siteBaseUrl, string publicMediaBaseUrl)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute)
            && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
        {
            return absolute.ToString();
        }

        var normalized = value.TrimStart('/');
        return normalized.StartsWith("images/", StringComparison.OrdinalIgnoreCase)
            ? $"{siteBaseUrl}/{normalized}"
            : $"{publicMediaBaseUrl}/{normalized}";
    }

    private sealed record SeoImage(string Url, string Title);
}
