namespace HomeService.Infrastructure;

public static class DatabaseConnectionStringResolver
{
    public static string Resolve(string? defaultConnection, string? databaseUrl, string? postgresUrl)
    {
        var urlConnection = FirstNotBlank(databaseUrl, postgresUrl);
        var directConnection = Normalize(defaultConnection);

        if (urlConnection is not null && IsLocalDevelopmentDefault(directConnection))
        {
            return ConvertPostgresUrl(urlConnection);
        }

        if (directConnection is not null)
        {
            return ConvertIfPostgresUrl(directConnection);
        }

        if (urlConnection is not null)
        {
            return ConvertPostgresUrl(urlConnection);
        }

        throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");
    }

    private static string ConvertIfPostgresUrl(string value)
    {
        if (value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return ConvertPostgresUrl(value);
        }

        return value;
    }

    private static string ConvertPostgresUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != "postgres" && uri.Scheme != "postgresql"))
        {
            return value;
        }

        var userInfoParts = uri.UserInfo.Split(':', 2);
        var username = userInfoParts.Length > 0 ? Uri.UnescapeDataString(userInfoParts[0]) : string.Empty;
        var password = userInfoParts.Length > 1 ? Uri.UnescapeDataString(userInfoParts[1]) : string.Empty;

        var parts = new List<string>
        {
            $"Host={EscapeValue(uri.Host)}",
            $"Port={(uri.Port > 0 ? uri.Port : 5432)}",
            $"Database={EscapeValue(uri.AbsolutePath.TrimStart('/'))}",
            $"Username={EscapeValue(username)}",
            $"Password={EscapeValue(password)}"
        };

        var sslMode = GetQueryValue(uri.Query, "sslmode") ?? GetQueryValue(uri.Query, "ssl");
        if (!string.IsNullOrWhiteSpace(sslMode))
        {
            parts.Add($"SSL Mode={EscapeValue(sslMode)}");
        }

        return string.Join(';', parts);
    }

    private static bool IsLocalDevelopmentDefault(string? connectionString)
    {
        return connectionString is not null
            && connectionString.Contains("Host=localhost", StringComparison.OrdinalIgnoreCase)
            && connectionString.Contains("Database=homeservice", StringComparison.OrdinalIgnoreCase)
            && connectionString.Contains("Username=homeservice", StringComparison.OrdinalIgnoreCase);
    }

    private static string? FirstNotBlank(params string?[] values)
    {
        foreach (var value in values)
        {
            var normalized = Normalize(value);
            if (normalized is not null)
            {
                return normalized;
            }
        }

        return null;
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string EscapeValue(string value)
    {
        return value.Contains(';', StringComparison.Ordinal) ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : value;
    }

    private static string? GetQueryValue(string query, string key)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var trimmed = query.TrimStart('?');
        foreach (var parameter in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = parameter.Split('=', 2);
            var parameterKey = Uri.UnescapeDataString(parts[0]);
            if (!string.Equals(parameterKey, key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
        }

        return null;
    }
}
