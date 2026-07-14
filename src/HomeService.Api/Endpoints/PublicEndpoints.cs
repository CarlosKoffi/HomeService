using HomeService.Application.Abstractions;
using HomeService.Contracts.Branding;
using HomeService.Contracts.Localization;
using HomeService.Contracts.Services;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Api.Endpoints;

public static class PublicEndpoints
{
    public static IEndpointRouteBuilder MapPublicEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "HomeService.Api" }))
            .WithName("HealthCheck");

        app.MapGet("/api/services", async (IAppDbContext db, CancellationToken cancellationToken) =>
        {
            var services = await db.Services
                .AsNoTracking()
                .OrderBy(service => service.Name)
                .Select(service => new ServiceSummaryResponse(
                    service.Id,
                    service.Name,
                    service.Description,
                    service.IconName,
                    service.Status.ToString(),
                    service.IsActive,
                    service.NormalPriceAmount,
                    service.PremiumPriceAmount,
                    service.Currency))
                .ToListAsync(cancellationToken);

            return Results.Ok(services);
        })
        .WithName("ListServices");

        app.MapGet("/api/translations", async (string? scope, string? language, string? country, IAppDbContext db, CancellationToken cancellationToken) =>
        {
            var languageCode = string.IsNullOrWhiteSpace(language) ? "fr" : language.Trim().ToLowerInvariant();
            var countryCode = string.IsNullOrWhiteSpace(country) ? "CI" : country.Trim().ToUpperInvariant();

            var query = db.TranslationValues
                .AsNoTracking()
                .Where(value => value.Language!.Code == languageCode)
                .Where(value => value.Country == null || value.Country.IsoCode == countryCode)
                .Where(value => value.TranslationKey!.IsActive);

            if (!string.IsNullOrWhiteSpace(scope))
            {
                query = query.Where(value => value.TranslationKey!.Scope == scope.Trim());
            }

            var translations = await query
                .OrderBy(value => value.TranslationKey!.Scope)
                .ThenBy(value => value.TranslationKey!.Key)
                .Select(value => new TranslationValueResponse(
                    value.TranslationKey!.Key,
                    value.TranslationKey.Scope,
                    value.Value))
                .ToListAsync(cancellationToken);

            return Results.Ok(translations);
        })
        .WithName("ListTranslations");

        app.MapGet("/api/translations/dictionary", async (string? scope, string? language, string? country, IAppDbContext db, CancellationToken cancellationToken) =>
        {
            var languageCode = string.IsNullOrWhiteSpace(language) ? "fr" : language.Trim().ToLowerInvariant();
            var countryCode = string.IsNullOrWhiteSpace(country) ? "CI" : country.Trim().ToUpperInvariant();

            var query = db.TranslationValues
                .AsNoTracking()
                .Where(value => value.Language!.Code == languageCode)
                .Where(value => value.Country == null || value.Country.IsoCode == countryCode)
                .Where(value => value.TranslationKey!.IsActive);

            if (!string.IsNullOrWhiteSpace(scope))
            {
                query = query.Where(value => value.TranslationKey!.Scope == scope.Trim());
            }

            var translations = await query
                .OrderBy(value => value.TranslationKey!.Scope)
                .ThenBy(value => value.TranslationKey!.Key)
                .Select(value => new
                {
                    value.TranslationKey!.Key,
                    value.Value
                })
                .ToDictionaryAsync(value => value.Key, value => value.Value, cancellationToken);

            return Results.Ok(translations);
        })
        .WithName("GetTranslationsDictionary");

        app.MapGet("/api/country-branding", async (string? country, IAppDbContext db, CancellationToken cancellationToken) =>
        {
            var countryCode = string.IsNullOrWhiteSpace(country) ? "CI" : country.Trim().ToUpperInvariant();
            var branding = await db.CountryBrandings
                .AsNoTracking()
                .Where(branding => branding.Country!.IsoCode == countryCode)
                .Select(branding => new CountryBrandingResponse(
                    branding.Country!.IsoCode,
                    branding.Country.Name,
                    branding.BrandName,
                    branding.PrimaryColor,
                    branding.SecondaryColor,
                    branding.AccentColor,
                    branding.HeroTitle,
                    branding.HeroSubtitle,
                    branding.HeroImageUrl,
                    branding.MotifStyle))
                .FirstOrDefaultAsync(cancellationToken);

            return branding is null ? Results.NotFound() : Results.Ok(branding);
        })
        .WithName("GetCountryBranding");

        return app;
    }
}
