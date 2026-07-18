using HomeService.Application.Abstractions;
using HomeService.Contracts.Localization;
using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminTranslationService(IAppDbContext db)
{
    public async Task<AdminTranslationListResponse> ListAsync(
        string? scope,
        string? search,
        string? language,
        CancellationToken cancellationToken)
    {
        var languageCode = NormalizeLanguage(language);
        var query = db.TranslationKeys
            .AsNoTracking()
            .Include(key => key.Values)
                .ThenInclude(value => value.Language)
            .Include(key => key.Values)
                .ThenInclude(value => value.Country)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(scope))
        {
            query = query.Where(key => key.Scope == scope.Trim());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(key =>
                key.Key.ToLower().Contains(term)
                || key.Scope.ToLower().Contains(term)
                || key.Description.ToLower().Contains(term)
                || key.Values.Any(value => value.Value.ToLower().Contains(term)));
        }

        var keys = await query
            .OrderBy(key => key.Scope)
            .ThenBy(key => key.Key)
            .Take(300)
            .ToListAsync(cancellationToken);

        var scopes = await db.TranslationKeys
            .AsNoTracking()
            .Select(key => key.Scope)
            .Distinct()
            .OrderBy(value => value)
            .ToListAsync(cancellationToken);

        var languages = await db.Languages
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderByDescending(item => item.IsDefault)
            .ThenBy(item => item.Code)
            .Select(item => item.Code)
            .ToListAsync(cancellationToken);

        var items = keys
            .Select(key =>
            {
                var value = key.Values
                    .Where(item => item.Language != null && item.Language.Code == languageCode)
                    .OrderByDescending(item => item.Country != null && item.Country.IsoCode == "CI")
                    .FirstOrDefault();

                return new AdminTranslationResponse(
                    key.Id,
                    value?.Id,
                    key.Key,
                    key.Scope,
                    key.Description,
                    languageCode,
                    value?.Value ?? string.Empty,
                    key.IsActive);
            })
            .ToList();

        return new AdminTranslationListResponse(items, scopes, languages);
    }

    public async Task<AdminTranslationResult> UpsertAsync(UpsertAdminTranslationRequest request, CancellationToken cancellationToken)
    {
        var keyName = request.Key.Trim();
        var scope = request.Scope.Trim();
        var description = request.Description.Trim();
        var languageCode = NormalizeLanguage(request.Language);
        var textValue = request.Value.Trim();

        if (string.IsNullOrWhiteSpace(keyName))
        {
            return AdminTranslationResult.ValidationFailed("La cle de traduction est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(scope))
        {
            return AdminTranslationResult.ValidationFailed("Le scope est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return AdminTranslationResult.ValidationFailed("La description est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(textValue))
        {
            return AdminTranslationResult.ValidationFailed("Le texte traduit est obligatoire.");
        }

        var language = await db.Languages
            .FirstOrDefaultAsync(item => item.Code == languageCode && item.IsActive, cancellationToken);
        if (language is null)
        {
            return AdminTranslationResult.ValidationFailed($"Langue inconnue: {languageCode}.");
        }

        var country = await db.Countries
            .FirstOrDefaultAsync(item => item.IsoCode == "CI", cancellationToken);

        var key = await db.TranslationKeys
            .FirstOrDefaultAsync(item => item.Key == keyName, cancellationToken);

        if (key is null)
        {
            key = new TranslationKey(keyName, description, scope);
            db.TranslationKeys.Add(key);
        }
        else
        {
            key.Update(description, scope);
        }

        var countryId = country?.Id;
        var value = await db.TranslationValues
            .FirstOrDefaultAsync(item =>
                    item.TranslationKeyId == key.Id
                    && item.LanguageId == language.Id
                    && item.CountryId == countryId,
                cancellationToken);

        if (value is null)
        {
            db.TranslationValues.Add(new TranslationValue(key.Id, language.Id, countryId, textValue));
        }
        else
        {
            value.UpdateValue(textValue);
        }

        return AdminTranslationResult.Ok();
    }

    private static string NormalizeLanguage(string? language)
    {
        return string.IsNullOrWhiteSpace(language) ? "fr" : language.Trim().ToLowerInvariant();
    }
}

public sealed record AdminTranslationResult(
    AdminTranslationStatus Status,
    string? Message)
{
    public static AdminTranslationResult Ok()
        => new(AdminTranslationStatus.Ok, null);

    public static AdminTranslationResult ValidationFailed(string message)
        => new(AdminTranslationStatus.ValidationFailed, message);
}

public enum AdminTranslationStatus
{
    Ok,
    ValidationFailed
}
