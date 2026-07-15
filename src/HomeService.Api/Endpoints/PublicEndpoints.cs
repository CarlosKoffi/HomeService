using HomeService.Api.Auditing;
using HomeService.Application.Abstractions;
using HomeService.Application.Auditing;
using HomeService.Application.Companies;
using HomeService.Contracts.Branding;
using HomeService.Contracts.Cms;
using HomeService.Contracts.Companies;
using HomeService.Contracts.Localization;
using HomeService.Contracts.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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
                .Include(service => service.Prestations)
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
                    service.Currency,
                    service.Prestations
                        .OrderBy(prestation => prestation.SortOrder)
                        .ThenBy(prestation => prestation.Name)
                        .Select(prestation => new ServicePrestationSummaryResponse(
                            prestation.Id,
                            prestation.Name,
                            prestation.Description,
                            prestation.SortOrder,
                            prestation.IsActive))
                        .ToList()))
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

        app.MapGet("/api/cms/company/home", async (
            string? language,
            string? country,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var languageCode = string.IsNullOrWhiteSpace(language) ? "fr" : language.Trim().ToLowerInvariant();
            var countryCode = string.IsNullOrWhiteSpace(country) ? "CI" : country.Trim().ToUpperInvariant();

            var pageVersionId = await db.CmsPages
                .AsNoTracking()
                .Where(page => page.Site!.Code == "company-public")
                .Where(page => page.Code == "home")
                .Where(page => page.Site!.DefaultCountry == null || page.Site.DefaultCountry.IsoCode == countryCode)
                .Select(page => page.Versions
                    .OrderByDescending(version => version.VersionNumber)
                    .Select(version => (Guid?)version.Id)
                    .FirstOrDefault())
                .FirstOrDefaultAsync(cancellationToken);

            if (pageVersionId is null)
            {
                return Results.NotFound(new { message = "Contenu CMS entreprise introuvable." });
            }

            var values = await db.CmsContentValues
                .AsNoTracking()
                .Where(value => value.Section!.PageVersionId == pageVersionId.Value)
                .Where(value => value.Language == null || value.Language.Code == languageCode)
                .Select(value => new CmsFieldProjection(
                    value.Section!.ComponentDefinition!.Key,
                    value.FieldKey,
                    value.TextValue,
                    value.JsonValue))
                .ToListAsync(cancellationToken);

            return Results.Ok(BuildCompanyHomeCmsResponse(values));
        })
        .WithName("GetCompanyHomeCmsContent")
        .Produces<CompanyHomeCmsResponse>()
        .Produces(StatusCodes.Status404NotFound);

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

    private static CompanyHomeCmsResponse BuildCompanyHomeCmsResponse(IReadOnlyList<CmsFieldProjection> values)
    {
        var hero = BuildFields(values, "HeroStandard");
        var steps = BuildFields(values, "StepsTimeline");
        var trusted = BuildFields(values, "TrustedLogos");
        var dashboard = BuildFields(values, "DashboardPreview");
        var faq = BuildFields(values, "FaqAccordion");
        var contact = BuildFields(values, "ContactForm");
        var footer = BuildFields(values, "FooterLinks");

        return new CompanyHomeCmsResponse(
            new CompanyHomeHeroCmsResponse(
                GetText(hero, "label", "Plateforme partenaire"),
                GetText(hero, "headline", "Recevez plus de missions. Developpez votre entreprise."),
                GetText(hero, "subtitle", "Kaza connecte les clients aux entreprises de services a domicile verifiees."),
                new CmsLinkResponse(
                    GetText(hero, "primaryCta.label", "Commencer"),
                    GetText(hero, "primaryCta.url", "register")),
                new CmsLinkResponse(
                    GetText(hero, "secondaryCta.label", "Voir le fonctionnement"),
                    GetText(hero, "secondaryCta.url", "#how")),
                GetText(hero, "image.url", "images/kaza-premium-hero.png"),
                GetText(hero, "image.alt", "Equipe Kaza en intervention chez un client"),
                GetJsonList(hero, "proofItems", ["Inscription gratuite", "Validation dossier", "Portail entreprise"])),
            new CompanyHomeStepsCmsResponse(
                GetText(steps, "label", "Comment ca marche"),
                GetText(steps, "headline", "Trois etapes, puis votre portail est pret."),
                GetText(steps, "subtitle", "Un parcours court pour verifier l'entreprise et demarrer avec une base claire."),
                GetJsonList(steps, "steps", [
                    new CmsStepResponse("01", "Compte", "Creez votre compte", "Renseignez votre entreprise, vos services et le contact responsable.", "images/kaza-how-step-1.png"),
                    new CmsStepResponse("02", "Verification", "Nous verifions votre dossier", "Kaza controle les informations pour securiser les clients et les missions.", "images/kaza-how-step-2.png"),
                    new CmsStepResponse("03", "Portail", "Travaillez depuis votre portail", "Ajoutez vos prestataires, recevez des demandes et suivez vos interventions.", "images/kaza-how-step-3.png")
                ])),
            new CompanyHomeTrustedCmsResponse(
                GetText(trusted, "headline", "Ils font confiance a Kaza"),
                GetJsonList(trusted, "items", ["Services verifies", "Entreprises locales", "Prestataires suivis", "Paiements traces", "Support partenaire"])),
            new CompanyHomeDashboardCmsResponse(
                GetText(dashboard, "label", "Dashboard"),
                GetText(dashboard, "headline", "Tout ce qui compte, lisible en un coup d'oeil."),
                GetText(dashboard, "subtitle", "Demandes, equipe, missions, documents et paiements restent au meme endroit."),
                GetJsonList(dashboard, "stats", [
                    new CmsDashboardStatResponse("Demandes", "12", "+4 cette semaine"),
                    new CmsDashboardStatResponse("Assignees", "8", "Equipe mobilisee"),
                    new CmsDashboardStatResponse("Paiements", "185k", "XOF suivis")
                ]),
                GetJsonList(dashboard, "requests", ["Menage a Cocody Riviera", "Jardinage a Marcory", "Nounou aux Deux Plateaux"]),
                GetJsonList(dashboard, "providers", ["Awa K. - Menage", "Jean M. - Jardinage", "Fatou C. - Nounou"])),
            new CompanyHomeFaqCmsResponse(
                GetText(faq, "label", "FAQ"),
                GetText(faq, "headline", "Foire aux questions"),
                GetJsonList(faq, "questions", [
                    new CmsFaqItemResponse("Comment sont verifiees les entreprises sur Kaza ?", "Nous verifions les informations de l'entreprise, les documents essentiels et le contact responsable avant l'activation complete."),
                    new CmsFaqItemResponse("L'inscription est-elle gratuite ?", "Oui. L'inscription est gratuite. Kaza applique ensuite une commission uniquement sur les missions realisees."),
                    new CmsFaqItemResponse("Puis-je refuser une demande client ?", "Oui. Votre entreprise reste libre d'accepter les demandes qui correspondent a son equipe, sa zone et ses disponibilites.")
                ])),
            new CompanyHomeContactCmsResponse(
                GetText(contact, "label", "Contact"),
                GetText(contact, "headline", "Vous voulez en parler avant de vous inscrire ?"),
                GetText(contact, "subtitle", "Laissez vos coordonnees. Nous vous rappelons pour voir comment Kaza peut aider votre entreprise."),
                GetJsonList(contact, "tags", ["Abidjan", "Services a domicile", "Partenariat entreprise"])),
            new CompanyHomeFooterCmsResponse(
                GetText(footer, "brandText", "La plateforme B2B pour connecter clients, entreprises et professionnels de confiance."),
                GetText(footer, "copyright", "(c) 2026 Kaza Technologies. Tous droits reserves."),
                GetText(footer, "baseline", "Concu pour l'Afrique de l'Ouest"),
                GetJsonList(footer, "columns", [
                    new CmsFooterColumnResponse("Produit", ["Plateforme", "Fonctionnement", "Tarifs", "Securite"]),
                    new CmsFooterColumnResponse("Entreprise", ["A propos", "Blog", "Carrieres", "Partenaires"]),
                    new CmsFooterColumnResponse("Ressources", ["Documentation", "Centre d'aide", "Communaute"]),
                    new CmsFooterColumnResponse("Legal", ["CGU", "Confidentialite", "Mentions legales"])
                ])));
    }

    private static Dictionary<string, string?> BuildFields(IReadOnlyList<CmsFieldProjection> values, string componentKey)
    {
        return values
            .Where(value => value.ComponentKey == componentKey)
            .GroupBy(value => value.FieldKey)
            .ToDictionary(
                group => group.Key,
                group => group.Select(value => value.JsonValue ?? value.TextValue).FirstOrDefault(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static string GetText(IReadOnlyDictionary<string, string?> fields, string key, string fallback)
    {
        return fields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
    }

    private static IReadOnlyList<T> GetJsonList<T>(IReadOnlyDictionary<string, string?> fields, string key, IReadOnlyList<T> fallback)
    {
        if (!fields.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        try
        {
            var result = JsonSerializer.Deserialize<IReadOnlyList<T>>(value, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
            return result.Count == 0 ? fallback : result;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    private sealed record CmsFieldProjection(
        string ComponentKey,
        string FieldKey,
        string? TextValue,
        string? JsonValue);
}
