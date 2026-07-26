using System.Text;

namespace HomeService.Infrastructure.Notifications;

public static class FirebaseCredentialsJsonResolver
{
    public static string? Resolve(string? rawJson, string? base64Json)
    {
        if (!string.IsNullOrWhiteSpace(base64Json))
        {
            try
            {
                var bytes = Convert.FromBase64String(base64Json.Trim());
                return Encoding.UTF8.GetString(bytes);
            }
            catch (FormatException)
            {
                return rawJson;
            }
        }

        return rawJson;
    }
}
