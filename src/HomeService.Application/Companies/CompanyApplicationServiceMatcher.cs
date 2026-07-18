using System.Globalization;
using System.Text;

namespace HomeService.Application.Companies;

public static class CompanyApplicationServiceMatcher
{
    public static IReadOnlyList<CompanyApplicationServiceMatchCandidate> FindCandidates(
        string rawName,
        IEnumerable<CompanyApplicationServiceCatalogItem> catalog)
    {
        var normalizedRawName = Normalize(rawName);
        if (string.IsNullOrWhiteSpace(normalizedRawName))
        {
            return [];
        }

        return catalog
            .Select(item => Score(item, normalizedRawName))
            .Where(candidate => candidate.Score >= 70)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.ServiceName)
            .ThenBy(candidate => candidate.ServicePrestationName)
            .Take(6)
            .ToList();
    }

    public static CompanyApplicationServiceMatchCandidate? FindBestCandidate(
        string rawName,
        IEnumerable<CompanyApplicationServiceCatalogItem> catalog)
    {
        return FindCandidates(rawName, catalog).FirstOrDefault(candidate => candidate.Score >= 85);
    }

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : ' ');
            }
        }

        return string.Join(' ', builder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static CompanyApplicationServiceMatchCandidate Score(CompanyApplicationServiceCatalogItem item, string normalizedRawName)
    {
        var serviceScore = ScoreText(normalizedRawName, item.NormalizedServiceName);
        var prestationScore = string.IsNullOrWhiteSpace(item.NormalizedServicePrestationName)
            ? 0
            : ScoreText(normalizedRawName, item.NormalizedServicePrestationName);
        var combinedScore = string.IsNullOrWhiteSpace(item.NormalizedServicePrestationName)
            ? 0
            : ScoreText(normalizedRawName, $"{item.NormalizedServiceName} {item.NormalizedServicePrestationName}");

        var score = Math.Max(serviceScore, Math.Max(prestationScore, combinedScore));
        var kind = item.ServicePrestationId.HasValue && Math.Max(prestationScore, combinedScore) >= serviceScore
            ? "Prestation"
            : "Service";

        return new CompanyApplicationServiceMatchCandidate(
            item.ServiceId,
            item.ServiceName,
            item.ServicePrestationId,
            item.ServicePrestationName,
            kind,
            score);
    }

    private static int ScoreText(string raw, string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return 0;
        }

        if (raw == candidate)
        {
            return 100;
        }

        if (raw.Contains(candidate, StringComparison.Ordinal) || candidate.Contains(raw, StringComparison.Ordinal))
        {
            return 90;
        }

        var rawTokens = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
        var candidateTokens = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
        if (rawTokens.Count == 0 || candidateTokens.Count == 0)
        {
            return 0;
        }

        var commonTokens = rawTokens.Count(token => candidateTokens.Contains(token));
        if (commonTokens == 0)
        {
            return 0;
        }

        var coverage = (double)commonTokens / Math.Min(rawTokens.Count, candidateTokens.Count);
        return coverage >= 1 ? 85 : (int)Math.Round(coverage * 80);
    }
}

public sealed record CompanyApplicationServiceCatalogItem(
    Guid ServiceId,
    string ServiceName,
    string NormalizedServiceName,
    Guid? ServicePrestationId,
    string? ServicePrestationName,
    string? NormalizedServicePrestationName);

public sealed record CompanyApplicationServiceMatchCandidate(
    Guid ServiceId,
    string ServiceName,
    Guid? ServicePrestationId,
    string? ServicePrestationName,
    string Kind,
    int Score);
