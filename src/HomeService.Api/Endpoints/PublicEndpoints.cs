using HomeService.Api.Auditing;
using HomeService.Application.Abstractions;
using HomeService.Application.Auditing;
using HomeService.Application.Companies;
using HomeService.Contracts.Branding;
using HomeService.Contracts.Companies;
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

        app.MapPost("/api/company-applications", async (
            HttpRequest httpRequest,
            CompanyApplicationUploadService uploadService,
            CompanyApplicationRegistrationService registrationService,
            IAppDbContext db,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            try
            {
                if (!httpRequest.HasFormContentType)
                {
                    return Results.BadRequest(new { message = "Le formulaire doit etre envoye au format multipart/form-data." });
                }

                logger.LogInformation("Company application submission received.");
                var form = await httpRequest.ReadFormAsync(cancellationToken);
                var request = new RegisterCompanyRequest(
                    GetFormValue(form, "companyName"),
                    GetOptionalFormValue(form, "registrationNumber"),
                    GetFormValue(form, "city"),
                    GetOptionalFormValue(form, "address"),
                    GetFormValue(form, "contactName"),
                    GetFormValue(form, "email"),
                    GetFormValue(form, "phoneNumber"),
                    GetFormValue(form, "password"),
                    GetFormValue(form, "confirmPassword"),
                    GetServices(form),
                    GetOptionalInt(form, "estimatedProviderCount"));

                var applicationId = Guid.NewGuid();
                var documents = await uploadService.SaveAsync(applicationId, form.Files, cancellationToken);
                var result = await registrationService.RegisterAsync(
                    request,
                    applicationId,
                    documents.Select(document => new CompanyApplicationUploadedDocument(
                            document.DocumentType,
                            document.OriginalFileName,
                            document.StoragePath,
                            document.ContentType))
                        .ToList(),
                    cancellationToken);

                if (result.Status == CompanyApplicationRegistrationStatus.ValidationFailed)
                {
                    return Results.BadRequest(new { message = result.Message, errors = result.Errors });
                }

                if (result.Status == CompanyApplicationRegistrationStatus.DuplicateEmail)
                {
                    return Results.BadRequest(new { message = result.Message });
                }

                var application = result.Application!;
                var company = result.Company!;
                logger.LogInformation("Stored {DocumentCount} company application documents for {ApplicationId}.", result.DocumentCount, application.Id);
                db.AuditLogEntries.Add(AuditLogFactory.Create(
                    AuditActor.Company(company.Id, company.Name),
                    "CompanyApplicationSubmitted",
                    nameof(HomeService.Domain.Entities.CompanyApplication),
                    application.Id,
                    "Demande entreprise creee depuis le formulaire public.",
                    HttpAuditContextFactory.Create(httpRequest),
                    after: new
                    {
                        application.CompanyName,
                        application.Email,
                        application.City,
                        result.ServiceCount,
                        result.DocumentCount,
                        application.Status
                    }));
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Company application {ApplicationId} saved.", application.Id);

                return Results.Created($"/api/admin/company-applications/{application.Id}", new { application.Id });
            }
            catch (InvalidOperationException exception)
            {
                logger.LogWarning(exception, "Company application submission rejected.");
                return Results.BadRequest(new { message = exception.Message });
            }
            catch (OperationCanceledException exception)
            {
                logger.LogWarning(exception, "Company application submission was cancelled while reading the form.");
                return Results.StatusCode(StatusCodes.Status499ClientClosedRequest);
            }
            catch (BadHttpRequestException exception)
            {
                logger.LogWarning(exception, "Company application submission was interrupted while reading uploaded files.");
                return Results.BadRequest(new { message = "L'envoi des pieces a ete interrompu. Verifiez la connexion puis relancez l'envoi." });
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Company application submission failed.");
                return Results.Problem(
                    title: "Impossible d'enregistrer la demande entreprise",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .WithName("RegisterCompanyApplication");

        return app;
    }

    private static string GetFormValue(IFormCollection form, string key)
    {
        return form.TryGetValue(key, out var value) ? value.ToString() : string.Empty;
    }

    private static string? GetOptionalFormValue(IFormCollection form, string key)
    {
        var value = GetFormValue(form, key);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static int? GetOptionalInt(IFormCollection form, string key)
    {
        return int.TryParse(GetFormValue(form, key), out var value) ? value : null;
    }

    private static IReadOnlyList<string> GetServices(IFormCollection form)
    {
        if (!form.TryGetValue("services", out var values))
        {
            return [];
        }

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => value!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
