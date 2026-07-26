using System.Text;

namespace HomeService.Infrastructure.Notifications;

public static class FirebaseCredentialsJsonResolver
{
    public static string? Resolve(string? rawJson, params string?[] base64JsonCandidates)
    {
        foreach (var base64Json in base64JsonCandidates)
        {
            if (string.IsNullOrWhiteSpace(base64Json))
            {
                continue;
            }

            var normalized = base64Json.Trim();
            try
            {
                var bytes = Convert.FromBase64String(normalized);
                return Encoding.UTF8.GetString(bytes);
            }
            catch (FormatException)
            {
            }
        }

        return rawJson;
    }
}
