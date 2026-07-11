using System.Text.Json;
using HomeService.Domain.Entities;

namespace HomeService.Application.Auditing;

public static class AuditLogFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> SensitiveNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "confirmPassword",
        "passwordHash",
        "token",
        "tokenHash",
        "activationLink",
        "rawToken",
        "secret",
        "apiKey",
        "accessToken",
        "refreshToken"
    };

    public static AuditLogEntry Create(
        AuditActor actor,
        string action,
        string entityType,
        Guid? entityId,
        string? summary,
        AuditRequestContext? context,
        object? before = null,
        object? after = null)
    {
        return new AuditLogEntry(
            actor.Type,
            actor.Id,
            actor.DisplayName,
            action,
            entityType,
            entityId,
            summary,
            SerializeSafe(before),
            SerializeSafe(after),
            context?.IpAddress,
            context?.UserAgent,
            context?.CorrelationId);
    }

    public static string? SerializeSafe(object? value)
    {
        if (value is null)
        {
            return null;
        }

        var json = JsonSerializer.Serialize(value, JsonOptions);
        using var document = JsonDocument.Parse(json);
        var redacted = RedactElement(document.RootElement);
        return JsonSerializer.Serialize(redacted, JsonOptions);
    }

    private static object? RedactElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(
                    property => property.Name,
                    property => SensitiveNames.Contains(property.Name) ? "***" : RedactElement(property.Value)),
            JsonValueKind.Array => element.EnumerateArray().Select(RedactElement).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var longValue)
                ? longValue
                : element.TryGetDecimal(out var decimalValue) ? decimalValue : element.GetRawText(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }
}
