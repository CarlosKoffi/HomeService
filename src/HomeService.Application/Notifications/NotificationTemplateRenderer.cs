using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace HomeService.Application.Notifications;

public static partial class NotificationTemplateRenderer
{
    public static string Render(string? template, string fallback, IReadOnlyDictionary<string, string?> variables)
    {
        var source = string.IsNullOrWhiteSpace(template) ? fallback : template;
        return VariablePattern().Replace(source, match =>
        {
            var key = match.Groups["key"].Value;
            return variables.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : match.Value;
        });
    }

    public static IReadOnlyDictionary<string, string?> Variables(params (string Key, string? Value)[] variables)
    {
        return new ReadOnlyDictionary<string, string?>(
            variables
                .GroupBy(variable => variable.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.OrdinalIgnoreCase));
    }

    [GeneratedRegex("\\{(?<key>[A-Za-z0-9_]+)\\}", RegexOptions.Compiled)]
    private static partial Regex VariablePattern();
}
