using System.Security.Cryptography;
using System.Text;

namespace HomeService.Company;

public static class SiteAccessGateExtensions
{
    public static IApplicationBuilder UseSiteAccessGate(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
            var expectedPassword = configuration["SITE_AUTH_PASSWORD"];

            if (string.IsNullOrWhiteSpace(expectedPassword))
            {
                await next(context);
                return;
            }

            var expectedUsername = configuration["SITE_AUTH_USERNAME"] ?? "admin";

            if (IsAuthorized(context.Request.Headers.Authorization, expectedUsername, expectedPassword))
            {
                await next(context);
                return;
            }

            context.Response.Headers.WWWAuthenticate = "Basic realm=\"HomeService\"";
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Authentication required.");
        });
    }

    private static bool IsAuthorized(string? authorizationHeader, string expectedUsername, string expectedPassword)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader) || !authorizationHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var encodedCredentials = authorizationHeader["Basic ".Length..].Trim();
            var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(encodedCredentials));
            var separatorIndex = credentials.IndexOf(':');

            if (separatorIndex <= 0)
            {
                return false;
            }

            var username = credentials[..separatorIndex];
            var password = credentials[(separatorIndex + 1)..];

            return SecureEquals(username, expectedUsername) && SecureEquals(password, expectedPassword);
        }
        catch
        {
            return false;
        }
    }

    private static bool SecureEquals(string value, string expected)
    {
        var valueBytes = Encoding.UTF8.GetBytes(value);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return valueBytes.Length == expectedBytes.Length && CryptographicOperations.FixedTimeEquals(valueBytes, expectedBytes);
    }
}
