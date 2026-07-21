using HomeService.Domain.Common;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Api;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HomeServiceDbContext>();

        await db.Database.MigrateAsync(cancellationToken);
        await EnsureMissionNumbersAsync(db, cancellationToken);
        await EnsureProviderServiceSchemaAsync(db, cancellationToken);
        await EnsureNotificationDeliveryRulesAsync(db, cancellationToken);
        await EnsureDefaultCommissionRulesAsync(db, cancellationToken);
        await NormalizeCatalogNamesAsync(db, cancellationToken);
        await SeedCountriesAsync(db, cancellationToken);
        await SeedCountryBrandingAsync(db, cancellationToken);
        await SeedLanguagesAsync(db, cancellationToken);
        await SeedServicesAsync(db, cancellationToken);
        await SeedServicePrestationsAsync(db, cancellationToken);
        await SeedDemoMissionsAsync(db, cancellationToken);
        await SeedAdminAccessAsync(db, cancellationToken);
        await SeedTranslationsAsync(db, cancellationToken);
        await SeedCmsFoundationAsync(db, cancellationToken);
        await SeedCompanyEditorialContentAsync(db, cancellationToken);
        await SeedProviderEditorialContentAsync(db, cancellationToken);
        await ApplyVisibleRebrandAsync(db, cancellationToken);
    }

    private static async Task SeedDemoMissionsAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "Sql", "037_seed_demo_missions.sql");
        if (!File.Exists(scriptPath))
        {
            return;
        }

        var script = await File.ReadAllTextAsync(scriptPath, cancellationToken);
        if (!string.IsNullOrWhiteSpace(script))
        {
            await db.Database.ExecuteSqlRawAsync(script, cancellationToken);
        }
    }

    private static async Task EnsureMissionNumbersAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "Missions"
                ADD COLUMN IF NOT EXISTS "MissionNumber" character varying(32);

            UPDATE "Missions"
            SET "MissionNumber" = upper(concat(
                'MIS-',
                to_char(coalesce("CreatedAt", now()), 'YYMMDD'),
                '-',
                substr(replace("Id"::text, '-', ''), 1, 8)
            ))
            WHERE "MissionNumber" IS NULL
               OR trim("MissionNumber") = '';

            ALTER TABLE "Missions"
                ALTER COLUMN "MissionNumber" SET NOT NULL;

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Missions_MissionNumber"
                ON "Missions" ("MissionNumber");
            """, cancellationToken);
    }

    private static async Task EnsureProviderServiceSchemaAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "ProviderServices"
                ADD COLUMN IF NOT EXISTS "CompanyId" uuid,
                ADD COLUMN IF NOT EXISTS "PriceTier" character varying(32) NOT NULL DEFAULT 'Normal',
                ADD COLUMN IF NOT EXISTS "PricingUnit" character varying(32) NOT NULL DEFAULT 'Hourly';

            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_name = 'ProviderServices'
                      AND column_name = 'HourlyRateAmount'
                ) THEN
                    ALTER TABLE "ProviderServices" ALTER COLUMN "HourlyRateAmount" SET DEFAULT 0;
                    ALTER TABLE "ProviderServices" ALTER COLUMN "HourlyRateAmount" DROP NOT NULL;
                END IF;

                IF EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_name = 'ProviderServices'
                      AND column_name = 'Currency'
                ) THEN
                    ALTER TABLE "ProviderServices" ALTER COLUMN "Currency" SET DEFAULT 'XOF';
                    ALTER TABLE "ProviderServices" ALTER COLUMN "Currency" DROP NOT NULL;
                END IF;
            END $$;

            UPDATE "ProviderServices" AS provider_service
            SET "CompanyId" = provider."CompanyId"
            FROM "Providers" AS provider
            WHERE provider_service."ProviderId" = provider."Id"
              AND provider_service."CompanyId" IS NULL
              AND provider."CompanyId" IS NOT NULL;

            ALTER TABLE "ProviderServices"
                DROP COLUMN IF EXISTS "HourlyRateAmount",
                DROP COLUMN IF EXISTS "Currency";

            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1
                    FROM "ProviderServices"
                    WHERE "CompanyId" IS NULL
                ) THEN
                    ALTER TABLE "ProviderServices" ALTER COLUMN "CompanyId" SET NOT NULL;
                END IF;
            END $$;
            """, cancellationToken);
    }

    private static async Task EnsureNotificationDeliveryRulesAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "NotificationDeliveryRules" (
                "Id" uuid NOT NULL,
                "EventKey" character varying(96) NOT NULL,
                "Label" character varying(180) NOT NULL,
                "Audience" character varying(32) NOT NULL,
                "PortalEnabled" boolean NOT NULL DEFAULT false,
                "MobileAppEnabled" boolean NOT NULL DEFAULT false,
                "EmailEnabled" boolean NOT NULL DEFAULT false,
                "WhatsAppEnabled" boolean NOT NULL DEFAULT false,
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_NotificationDeliveryRules" PRIMARY KEY ("Id")
            );

            ALTER TABLE "NotificationDeliveryRules"
                ADD COLUMN IF NOT EXISTS "EventKey" character varying(96) NOT NULL DEFAULT '',
                ADD COLUMN IF NOT EXISTS "Label" character varying(180) NOT NULL DEFAULT '',
                ADD COLUMN IF NOT EXISTS "Audience" character varying(32) NOT NULL DEFAULT 'Company',
                ADD COLUMN IF NOT EXISTS "PortalEnabled" boolean NOT NULL DEFAULT false,
                ADD COLUMN IF NOT EXISTS "MobileAppEnabled" boolean NOT NULL DEFAULT false,
                ADD COLUMN IF NOT EXISTS "EmailEnabled" boolean NOT NULL DEFAULT false,
                ADD COLUMN IF NOT EXISTS "WhatsAppEnabled" boolean NOT NULL DEFAULT false,
                ADD COLUMN IF NOT EXISTS "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
                ADD COLUMN IF NOT EXISTS "UpdatedAt" timestamp with time zone NULL;

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_NotificationDeliveryRules_EventKey"
                ON "NotificationDeliveryRules" ("EventKey");

            CREATE INDEX IF NOT EXISTS "IX_NotificationDeliveryRules_Audience_EventKey"
                ON "NotificationDeliveryRules" ("Audience", "EventKey");

            INSERT INTO "NotificationDeliveryRules"
                ("Id", "EventKey", "Label", "Audience", "PortalEnabled", "MobileAppEnabled", "EmailEnabled", "WhatsAppEnabled", "CreatedAt", "UpdatedAt")
            VALUES
                ('3c8b462a-5d7f-43f2-9c32-720b0b5e2001', 'CompanyDocumentRejected', 'Piece entreprise refusee', 'Company', true, false, true, true, now(), now()),
                ('3c8b462a-5d7f-43f2-9c32-720b0b5e2002', 'CompanyDocumentNeedsReplacement', 'Complement requis sur dossier entreprise', 'Company', true, false, true, true, now(), now()),
                ('3c8b462a-5d7f-43f2-9c32-720b0b5e2003', 'CompanyDocumentReopened', 'Piece entreprise reouverte', 'Company', true, false, true, true, now(), now()),
                ('3c8b462a-5d7f-43f2-9c32-720b0b5e2004', 'CompanyApplicationRejected', 'Dossier entreprise refuse', 'Company', true, false, true, true, now(), now()),
                ('3c8b462a-5d7f-43f2-9c32-720b0b5e2005', 'CompanyApplicationReopened', 'Dossier entreprise reouvert', 'Company', true, false, true, true, now(), now()),
                ('3c8b462a-5d7f-43f2-9c32-720b0b5e2006', 'CompanyApplicationMoreInformationRequested', 'Complement requis sur dossier entreprise', 'Company', true, false, true, true, now(), now()),
                ('3c8b462a-5d7f-43f2-9c32-720b0b5e2007', 'CompanyApplicationApproved', 'Dossier entreprise valide', 'Company', true, false, true, true, now(), now()),
                ('3c8b462a-5d7f-43f2-9c32-720b0b5e2008', 'CompanyActivationLinkCreated', 'Lien d''activation entreprise', 'Company', true, false, true, true, now(), now()),
                ('3c8b462a-5d7f-43f2-9c32-720b0b5e2009', 'InterimCandidateReceived', 'Nouvelle demande interimaire', 'Company', true, false, false, false, now(), now()),
                ('3c8b462a-5d7f-43f2-9c32-720b0b5e2010', 'InterimCandidateApproved', 'Candidature interimaire acceptee', 'Provider', false, true, false, true, now(), now()),
                ('3c8b462a-5d7f-43f2-9c32-720b0b5e2011', 'MissionAssignedToProvider', 'Mission affectee au prestataire', 'Provider', false, true, false, true, now(), now()),
                ('3c8b462a-5d7f-43f2-9c32-720b0b5e2012', 'MissionQuoteSentToCustomer', 'Devis mission envoye au client', 'Customer', false, true, true, true, now(), now()),
                ('3c8b462a-5d7f-43f2-9c32-720b0b5e2013', 'MissionStatusChanged', 'Suivi de mission', 'Mixed', true, true, false, false, now(), now())
            ON CONFLICT ("EventKey") DO UPDATE
            SET "Label" = EXCLUDED."Label",
                "Audience" = EXCLUDED."Audience",
                "PortalEnabled" = CASE WHEN EXCLUDED."Audience" IN ('Company', 'Mixed') THEN true ELSE false END,
                "MobileAppEnabled" = CASE WHEN EXCLUDED."Audience" IN ('Provider', 'Customer', 'Mixed') THEN true ELSE false END,
                "UpdatedAt" = now();

            UPDATE "NotificationDeliveryRules"
            SET "PortalEnabled" = CASE WHEN "Audience" IN ('Company', 'Mixed') THEN true ELSE false END,
                "MobileAppEnabled" = CASE WHEN "Audience" IN ('Provider', 'Customer', 'Mixed') THEN true ELSE false END,
                "UpdatedAt" = now()
            WHERE "PortalEnabled" <> CASE WHEN "Audience" IN ('Company', 'Mixed') THEN true ELSE false END
               OR "MobileAppEnabled" <> CASE WHEN "Audience" IN ('Provider', 'Customer', 'Mixed') THEN true ELSE false END;
            """, cancellationToken);
    }

    private static async Task EnsureDefaultCommissionRulesAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            UPDATE "CommissionRules"
            SET "Name" = 'Commission mise en relation wélé',
                "RateBasisPoints" = 1500,
                "FixedAmount" = 0,
                "Currency" = 'XOF',
                "IsActive" = true,
                "EffectiveUntil" = NULL,
                "UpdatedAt" = now()
            WHERE "Target" = 'PlatformConnection'
              AND "ServiceId" IS NULL
              AND "ServicePrestationId" IS NULL
              AND "CompanyId" IS NULL
              AND "AssignmentSource" IS NULL;

            INSERT INTO "CommissionRules"
                ("Id", "Name", "Target", "ServiceId", "ServicePrestationId", "CompanyId", "AssignmentSource",
                 "RateBasisPoints", "FixedAmount", "Currency", "EffectiveFrom", "EffectiveUntil", "IsActive",
                 "CreatedAt", "UpdatedAt")
            SELECT gen_random_uuid(), 'Commission mise en relation wélé', 'PlatformConnection',
                   NULL, NULL, NULL, NULL, 1500, 0, 'XOF', now(), NULL, true, now(), now()
            WHERE NOT EXISTS (
                SELECT 1
                FROM "CommissionRules"
                WHERE "Target" = 'PlatformConnection'
                  AND "ServiceId" IS NULL
                  AND "ServicePrestationId" IS NULL
                  AND "CompanyId" IS NULL
                  AND "AssignmentSource" IS NULL
            );
            """, cancellationToken);
    }

    private static async Task NormalizeCatalogNamesAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE OR REPLACE FUNCTION wele_normalize_catalog_name(value text)
            RETURNS text
            LANGUAGE sql
            IMMUTABLE
            AS $$
                SELECT trim(regexp_replace(
                    translate(
                        lower(coalesce(value, '')),
                        'àáâäãåçèéêëìíîïñòóôöõùúûüýÿ',
                        'aaaaaaceeeeiiiinooooouuuuyy'
                    ),
                    '[^a-z0-9]+',
                    ' ',
                    'g'
                ));
            $$;

            WITH normalized_services AS (
                SELECT
                    "Id",
                    wele_normalize_catalog_name("Name") AS "NextNormalizedName"
                FROM "Services"
            ),
            safe_services AS (
                SELECT item."Id", item."NextNormalizedName"
                FROM normalized_services item
                WHERE item."NextNormalizedName" <> ''
                  AND NOT EXISTS (
                      SELECT 1
                      FROM normalized_services duplicate
                      WHERE duplicate."Id" <> item."Id"
                        AND duplicate."NextNormalizedName" = item."NextNormalizedName"
                  )
            )
            UPDATE "Services" service
            SET "NormalizedName" = safe."NextNormalizedName"
            FROM safe_services safe
            WHERE service."Id" = safe."Id"
              AND service."NormalizedName" <> safe."NextNormalizedName";

            WITH normalized_prestations AS (
                SELECT
                    "Id",
                    "ServiceId",
                    wele_normalize_catalog_name("Name") AS "NextNormalizedName"
                FROM "ServicePrestations"
            ),
            safe_prestations AS (
                SELECT item."Id", item."NextNormalizedName"
                FROM normalized_prestations item
                WHERE item."NextNormalizedName" <> ''
                  AND NOT EXISTS (
                      SELECT 1
                      FROM normalized_prestations duplicate
                      WHERE duplicate."Id" <> item."Id"
                        AND duplicate."ServiceId" = item."ServiceId"
                        AND duplicate."NextNormalizedName" = item."NextNormalizedName"
                  )
            )
            UPDATE "ServicePrestations" prestation
            SET "NormalizedName" = safe."NextNormalizedName"
            FROM safe_prestations safe
            WHERE prestation."Id" = safe."Id"
              AND prestation."NormalizedName" <> safe."NextNormalizedName";

            UPDATE "CompanyApplicationServices"
            SET "NormalizedName" = wele_normalize_catalog_name("RawName")
            WHERE wele_normalize_catalog_name("RawName") <> ''
              AND "NormalizedName" <> wele_normalize_catalog_name("RawName");

            DROP FUNCTION wele_normalize_catalog_name(text);
            """, cancellationToken);
    }

    private static async Task ApplyVisibleRebrandAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            UPDATE "CountryBrandings"
            SET "BrandName" = 'wélé',
                "UpdatedAt" = now()
            WHERE "BrandName" IN ('Kaza', 'ProxiPro', 'ProxiPro CI', 'Kaza CI');

            UPDATE "CmsSites"
            SET "Name" = CASE
                    WHEN "Code" = 'company' THEN 'wélé entreprises'
                    WHEN "Code" = 'provider' THEN 'wélé prestataires'
                    WHEN "Code" = 'client' THEN 'wélé clients'
                    ELSE replace(replace("Name", 'Kaza', 'wélé'), 'ProxiPro', 'wélé')
                END,
                "UpdatedAt" = now()
            WHERE "Name" LIKE '%Kaza%'
               OR "Name" LIKE '%ProxiPro%';

            UPDATE "CmsPages"
            SET "InternalName" = replace(replace("InternalName", 'Kaza', 'wélé'), 'ProxiPro', 'wélé'),
                "UpdatedAt" = now()
            WHERE "InternalName" LIKE '%Kaza%'
               OR "InternalName" LIKE '%ProxiPro%';

            UPDATE "CmsPageTranslations"
            SET "Title" = replace(replace("Title", 'Kaza', 'wélé'), 'ProxiPro', 'wélé'),
                "SeoTitle" = CASE
                    WHEN "SeoTitle" IS NULL THEN NULL
                    ELSE replace(replace("SeoTitle", 'Kaza', 'wélé'), 'ProxiPro', 'wélé')
                END,
                "MetaDescription" = CASE
                    WHEN "MetaDescription" IS NULL THEN NULL
                    ELSE replace(replace("MetaDescription", 'Kaza', 'wélé'), 'ProxiPro', 'wélé')
                END,
                "UpdatedAt" = now()
            WHERE "Title" LIKE '%Kaza%'
               OR "Title" LIKE '%ProxiPro%'
               OR "SeoTitle" LIKE '%Kaza%'
               OR "SeoTitle" LIKE '%ProxiPro%'
               OR "MetaDescription" LIKE '%Kaza%'
               OR "MetaDescription" LIKE '%ProxiPro%';

            UPDATE "CmsContentValues"
            SET "TextValue" = replace(
                    replace(
                        replace("TextValue", 'Kaza Technologies', 'wélé Technologies'),
                        'Kaza',
                        'wélé'),
                    'ProxiPro',
                    'wélé'),
                "UpdatedAt" = now()
            WHERE "TextValue" LIKE '%Kaza%'
               OR "TextValue" LIKE '%ProxiPro%';

            UPDATE "CmsContentValues"
            SET "TextValue" = replace("TextValue", 'images/kaza-', 'images/wele-'),
                "UpdatedAt" = now()
            WHERE "TextValue" LIKE '%images/kaza-%';

            UPDATE "CmsContentValues"
            SET "JsonValue" = replace(
                    replace(
                        replace("JsonValue"::text, 'Kaza Technologies', 'wélé Technologies'),
                        'Kaza',
                        'wélé'),
                    'ProxiPro',
                    'wélé')::jsonb,
                "UpdatedAt" = now()
            WHERE "JsonValue"::text LIKE '%Kaza%'
               OR "JsonValue"::text LIKE '%ProxiPro%';

            UPDATE "CmsContentValues"
            SET "JsonValue" = replace("JsonValue"::text, 'images/kaza-', 'images/wele-')::jsonb,
                "UpdatedAt" = now()
            WHERE "JsonValue"::text LIKE '%images/kaza-%';

            UPDATE "TranslationValues"
            SET "Value" = replace(replace("Value", 'Kaza', 'wélé'), 'ProxiPro', 'wélé'),
                "UpdatedAt" = now()
            WHERE "Value" LIKE '%Kaza%'
               OR "Value" LIKE '%ProxiPro%';
            """, cancellationToken);
    }

    private static async Task SeedCountriesAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Countries.AnyAsync(cancellationToken))
        {
            return;
        }

        db.Countries.AddRange(
            new Country("CI", "Cote d'Ivoire", "XOF", isLaunchCountry: true),
            new Country("SN", "Senegal", "XOF"),
            new Country("BJ", "Benin", "XOF"),
            new Country("TG", "Togo", "XOF"));

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedLanguagesAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Languages.AnyAsync(cancellationToken))
        {
            return;
        }

        db.Languages.AddRange(
            new Language("fr", "Francais", isDefault: true),
            new Language("en", "Anglais"));

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedCountryBrandingAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        var coteDIvoire = await db.Countries.FirstAsync(country => country.IsoCode == "CI", cancellationToken);
        var hasBranding = await db.CountryBrandings.AnyAsync(branding => branding.CountryId == coteDIvoire.Id, cancellationToken);
        if (hasBranding)
        {
            return;
        }

        db.CountryBrandings.Add(new CountryBranding(
            coteDIvoire.Id,
            "wélé CI",
            "#0f9f7a",
            "#ffffff",
            "#f97316",
            "Le service a domicile en toute confiance",
            "Une plateforme pensee pour la Cote d'Ivoire: entreprises verifiees, prestataires suivis et services a domicile plus fiables.",
            null,
            "flag-ribbon"));

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedServicesAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        var seeds = new[]
        {
            new SeededService("Menage a domicile", "Entretien courant du domicile, nettoyage, rangement et aide ponctuelle.", "sparkles", 3500, 5000, "XOF"),
            new SeededService("Jardinage", "Entretien jardin, taille simple, arrosage et travaux exterieurs legers.", "sprout", 4500, 6500, "XOF"),
            new SeededService("Electricite", "Petites interventions electriques, diagnostic simple et remise en service.", "zap", 5000, 8000, "XOF"),
            new SeededService("Blanchisserie", "Lavage, repassage et entretien du linge pour particuliers et familles.", "shirt", 2500, 4500, "XOF"),
            new SeededService("Depannage auto", "Assistance auto de proximite pour les urgences simples et depannages courants.", "car", 7000, 12000, "XOF"),
            new SeededService("Nounou", "Garde d'enfant a domicile par un prestataire recommande et rattache a une entreprise validee.", "baby", 4000, 6500, "XOF")
        };

        var existingServices = await db.Services.ToListAsync(cancellationToken);
        foreach (var seed in seeds)
        {
            var normalizedName = NormalizeSeedValue(seed.Name);
            var service = existingServices.FirstOrDefault(item => item.NormalizedName == normalizedName);
            if (service is null)
            {
                service = new Service(seed.Name, seed.Description, createdByCompanyId: null);
                db.Services.Add(service);
                existingServices.Add(service);
            }

            service.UpdatePricing(seed.NormalPriceAmount, seed.PremiumPriceAmount, seed.Currency);
            service.UpdateIcon(seed.IconName);
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task SeedServicePrestationsAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        var services = await db.Services
            .Where(service => service.NormalizedName == "jardinage"
                || service.NormalizedName == "menage a domicile"
                || service.NormalizedName == "nounou"
                || service.NormalizedName == "electricite"
                || service.NormalizedName == "blanchisserie"
                || service.NormalizedName == "depannage auto")
            .Select(service => new { service.Id, service.NormalizedName })
            .ToListAsync(cancellationToken);

        if (services.Count == 0)
        {
            return;
        }

        var serviceIds = services.Select(service => service.Id).ToArray();
        var existingPrestations = await db.ServicePrestations
            .Where(prestation => serviceIds.Contains(prestation.ServiceId))
            .ToListAsync(cancellationToken);

        var existingKeySet = existingPrestations
            .Select(prestation => $"{prestation.ServiceId:N}:{prestation.NormalizedName}")
            .ToHashSet(StringComparer.Ordinal);

        var seeds = new[]
        {
            new SeededServicePrestation("jardinage", "Tondre le gazon", "Coupe et entretien simple de pelouse.", 10, 4500, 6500, "XOF"),
            new SeededServicePrestation("jardinage", "Tailler une haie", "Taille legere et remise en forme des haies.", 20, 5500, 7500, "XOF"),
            new SeededServicePrestation("jardinage", "Desherbage", "Nettoyage des mauvaises herbes sur les zones indiquees.", 30, 3500, 5000, "XOF"),
            new SeededServicePrestation("jardinage", "Arrosage et entretien plantes", "Arrosage, controle visuel et entretien leger des plantes.", 40, 3000, 4500, "XOF"),
            new SeededServicePrestation("jardinage", "Ramassage feuilles", "Ramassage des feuilles et nettoyage leger des allees.", 50, 3000, 4500, "XOF"),
            new SeededServicePrestation("jardinage", "Nettoyage terrasse exterieure", "Balayage et nettoyage simple de terrasse ou cour.", 60, 4500, 6500, "XOF"),
            new SeededServicePrestation("menage a domicile", "Menage regulier", "Entretien courant du domicile.", 10, 3500, 5000, "XOF"),
            new SeededServicePrestation("menage a domicile", "Grand nettoyage", "Nettoyage complet d'un logement ou d'une grande piece.", 15, 6000, 8500, "XOF"),
            new SeededServicePrestation("menage a domicile", "Nettoyage apres travaux", "Nettoyage renforce apres petits travaux ou renovation.", 20, 5000, 7000, "XOF"),
            new SeededServicePrestation("menage a domicile", "Nettoyage vitres", "Nettoyage simple des vitres accessibles.", 30, 3000, 4500, "XOF"),
            new SeededServicePrestation("menage a domicile", "Nettoyage cuisine", "Nettoyage detaille de cuisine, plans de travail et surfaces.", 40, 4000, 6000, "XOF"),
            new SeededServicePrestation("menage a domicile", "Nettoyage sanitaires", "Nettoyage detaille salle d'eau, WC et surfaces sanitaires.", 50, 4000, 6000, "XOF"),
            new SeededServicePrestation("nounou", "Garde ponctuelle", "Garde d'enfant sur une plage horaire courte.", 10, 4000, 6500, "XOF"),
            new SeededServicePrestation("nounou", "Garde apres ecole", "Presence et accompagnement apres l'ecole.", 20, 4500, 7000, "XOF"),
            new SeededServicePrestation("electricite", "Diagnostic panne electrique", "Recherche simple de panne et conseil d'intervention.", 10, 6000, 9000, "XOF"),
            new SeededServicePrestation("electricite", "Remplacement prise ou interrupteur", "Remplacement d'une prise, interrupteur ou point simple.", 20, 5000, 7500, "XOF"),
            new SeededServicePrestation("electricite", "Installation luminaire", "Pose ou remplacement d'un luminaire existant.", 30, 6000, 9000, "XOF"),
            new SeededServicePrestation("electricite", "Remise en service disjoncteur", "Controle et remise en service simple apres coupure.", 40, 5000, 8000, "XOF"),
            new SeededServicePrestation("electricite", "Depannage court-circuit simple", "Intervention sur panne courte et localisee.", 50, 8000, 12000, "XOF"),
            new SeededServicePrestation("electricite", "Installation ventilateur plafond", "Pose simple d'un ventilateur sur attente electrique existante.", 60, 10000, 15000, "XOF"),
            new SeededServicePrestation("blanchisserie", "Lavage et pliage", "Lavage, sechage et pliage du linge courant.", 10, 2500, 4000, "XOF"),
            new SeededServicePrestation("blanchisserie", "Repassage", "Repassage de vetements courants.", 20, 3000, 4500, "XOF"),
            new SeededServicePrestation("blanchisserie", "Linge de maison", "Entretien draps, serviettes et linge de maison.", 30, 3500, 5500, "XOF"),
            new SeededServicePrestation("blanchisserie", "Pressing tenue", "Entretien de tenue, robe, chemise ou costume selon disponibilite.", 40, 5000, 8000, "XOF"),
            new SeededServicePrestation("blanchisserie", "Detache simple", "Traitement simple de tache avant lavage.", 50, 3000, 5000, "XOF"),
            new SeededServicePrestation("depannage auto", "Changement batterie", "Remplacement ou assistance batterie sur place.", 10, 7000, 12000, "XOF"),
            new SeededServicePrestation("depannage auto", "Aide crevaison", "Aide au changement de roue ou pose de roue de secours.", 20, 6000, 10000, "XOF"),
            new SeededServicePrestation("depannage auto", "Demarrage avec cables", "Assistance demarrage avec cables ou booster.", 30, 6000, 9000, "XOF"),
            new SeededServicePrestation("depannage auto", "Diagnostic panne demarrage", "Controle simple quand le vehicule ne demarre pas.", 40, 8000, 12000, "XOF"),
            new SeededServicePrestation("depannage auto", "Carburant urgence", "Assistance en cas de panne seche dans la zone couverte.", 50, 6000, 10000, "XOF"),
            new SeededServicePrestation("depannage auto", "Remorquage partenaire", "Mise en relation ou assistance remorquage selon disponibilite.", 60, 15000, 25000, "XOF")
        };

        foreach (var seed in seeds)
        {
            var service = services.FirstOrDefault(item => item.NormalizedName == seed.ServiceNormalizedName);
            if (service is null)
            {
                continue;
            }

            var normalizedPrestationName = NormalizeSeedValue(seed.Name);
            var existing = existingPrestations.FirstOrDefault(prestation =>
                prestation.ServiceId == service.Id && prestation.NormalizedName == normalizedPrestationName);
            if (existing is not null)
            {
                existing.UpdatePricing(seed.NormalPriceAmount, seed.PremiumPriceAmount, seed.Currency);
                continue;
            }

            db.ServicePrestations.Add(new ServicePrestation(
                service.Id,
                seed.Name,
                seed.Description,
                seed.SortOrder,
                seed.NormalPriceAmount,
                seed.PremiumPriceAmount,
                seed.Currency));
            existingKeySet.Add($"{service.Id:N}:{normalizedPrestationName}");
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static string NormalizeSeedValue(string value)
    {
        return CatalogNameNormalizer.Normalize(value);
    }

    private sealed record SeededService(
        string Name,
        string Description,
        string IconName,
        int NormalPriceAmount,
        int PremiumPriceAmount,
        string Currency);

    private sealed record SeededServicePrestation(
        string ServiceNormalizedName,
        string Name,
        string? Description,
        int SortOrder,
        int NormalPriceAmount,
        int PremiumPriceAmount,
        string Currency);

    private static async Task SeedAdminAccessAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        if (!await db.AdminModules.AnyAsync(cancellationToken))
        {
            db.AdminModules.AddRange(
                new AdminModule(AdminModuleKey.Dashboard, "Tableau de bord", "Vue de synthese du back-office entreprise.", 10),
                new AdminModule(AdminModuleKey.CompanyApplications, "Demandes entreprises", "Validation des inscriptions, documents et activation des entreprises.", 20),
                new AdminModule(AdminModuleKey.Services, "Services", "Gestion du catalogue plat et des services proposes par les entreprises.", 30),
                new AdminModule(AdminModuleKey.Localization, "Traductions", "Gestion des langues et textes traduisibles.", 40),
                new AdminModule(AdminModuleKey.AdminAccess, "Acces et roles", "Gestion des roles, modules et permissions admin.", 50));

            await db.SaveChangesAsync(cancellationToken);
        }

        if (!await db.AdminRoles.AnyAsync(cancellationToken))
        {
            db.AdminRoles.AddRange(
                new AdminRole("Super admin", "Acces complet a tous les modules et aux permissions."),
                new AdminRole("Validation entreprises", "Peut traiter les demandes d'inscription entreprise."),
                new AdminRole("Contenu et traduction", "Peut gerer les textes, pays et langues."));

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task SeedTranslationsAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        var french = await db.Languages.FirstAsync(language => language.Code == "fr", cancellationToken);
        var coteDIvoire = await db.Countries.FirstAsync(country => country.IsoCode == "CI", cancellationToken);

        var translations = new[]
        {
            new TranslationSeed("company.home.hero.title", "Company", "Titre hero portail entreprise", "Le service a domicile en toute confiance"),
            new TranslationSeed("company.home.hero.subtitle", "Company", "Sous-titre hero portail entreprise", "Inscrivez votre entreprise, faites valider vos prestataires et developpez vos missions a domicile."),
            new TranslationSeed("company.register.title", "Company", "Titre inscription entreprise", "Demande d'inscription"),
            new TranslationSeed("company.register.description", "Company", "Introduction formulaire inscription", "Ce formulaire permet a votre entreprise de demander son acces wélé. Notre equipe verifiera les informations et les pieces fournies."),
            new TranslationSeed("company.register.submit", "Company", "Bouton envoyer demande", "Envoyer la demande"),
            new TranslationSeed("company.register.success", "Company", "Confirmation demande envoyee", "Demande envoyee. Notre equipe va verifier votre dossier."),
            new TranslationSeed("company.employees.form.title", "Company", "Titre formulaire ajout employe", "Nouvel employe"),
            new TranslationSeed("company.employees.form.description", "Company", "Aide formulaire ajout employe", "Renseignez les informations essentielles. Les prix des services sont fixes par la plateforme."),
            new TranslationSeed("company.employees.services.title", "Company", "Titre selection services employe", "Services maitrises"),
            new TranslationSeed("company.employees.services.description", "Company", "Aide selection services employe", "Selectionnez les services que ce prestataire peut realiser. Les tarifs normal et premium sont fixes dans l'administration."),
            new TranslationSeed("company.employees.upload.photo", "Company", "Upload photo employe", "Photo du prestataire"),
            new TranslationSeed("company.employees.upload.identity", "Company", "Upload piece identite employe", "Piece d'identite"),
            new TranslationSeed("company.employees.upload.diploma", "Company", "Upload diplome employe", "Diplome ou certificat"),
            new TranslationSeed("company.employees.upload.choose", "Company", "Bouton selection fichier employe", "Selectionner"),
            new TranslationSeed("admin.dashboard.title", "Admin", "Titre dashboard admin", "Centre de controle entreprise"),
            new TranslationSeed("admin.companyApplications.title", "Admin", "Titre file demandes entreprise", "Demandes entreprises"),
            new TranslationSeed("admin.companyApplications.empty", "Admin", "Message liste vide", "Aucune demande entreprise pour le moment."),
            new TranslationSeed("admin.localization.title", "Admin", "Titre page traductions", "Traductions"),
            new TranslationSeed("admin.access.title", "Admin", "Titre acces roles", "Acces & roles"),
            new TranslationSeed("common.loading", "Common", "Message chargement generique", "Chargement en cours..."),
            new TranslationSeed("common.save", "Common", "Action sauvegarder", "Sauver"),
            new TranslationSeed("common.validate", "Common", "Action valider", "Valider"),
            new TranslationSeed("common.reject", "Common", "Action rejeter", "Rejeter")
        };

        foreach (var seed in translations)
        {
            var key = await db.TranslationKeys
                .Include(item => item.Values)
                .FirstOrDefaultAsync(item => item.Key == seed.Key, cancellationToken);

            if (key is null)
            {
                key = new TranslationKey(seed.Key, seed.Description, seed.Scope);
                db.TranslationKeys.Add(key);
                await db.SaveChangesAsync(cancellationToken);
            }

            var hasValue = await db.TranslationValues.AnyAsync(
                value => value.TranslationKeyId == key.Id
                    && value.LanguageId == french.Id
                    && value.CountryId == coteDIvoire.Id,
                cancellationToken);

            if (!hasValue)
            {
                db.TranslationValues.Add(new TranslationValue(key.Id, french.Id, coteDIvoire.Id, seed.Value));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedCmsFoundationAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        if (await db.CmsSites.AnyAsync(cancellationToken))
        {
            return;
        }

        var french = await db.Languages.FirstAsync(language => language.Code == "fr", cancellationToken);
        var coteDIvoire = await db.Countries.FirstAsync(country => country.IsoCode == "CI", cancellationToken);

        var hero = new CmsComponentDefinition("HeroStandard", "Hero standard", 1, "Section d'ouverture sobre avec titre, texte court et appels a l'action.");
        var steps = new CmsComponentDefinition("StepsTimeline", "Parcours en etapes", 1, "Explication courte d'un processus en trois a six etapes.");
        var trusted = new CmsComponentDefinition("TrustedLogos", "Preuve sociale", 1, "Bande de references ou preuves de confiance en style premium.");
        var services = new CmsComponentDefinition("ServicesList", "Liste de services", 1, "Liste structuree de services ou metiers affichables.");
        var dashboard = new CmsComponentDefinition("DashboardPreview", "Apercu dashboard", 1, "Mockup produit avec indicateurs, activite et donnees de demonstration.");
        var faq = new CmsComponentDefinition("FaqAccordion", "Foire aux questions", 1, "Questions/reponses simples avec ouverture progressive.");
        var cta = new CmsComponentDefinition("CallToAction", "Appel a l'action", 1, "Bloc final ou contextuel pour pousser une action principale.");
        var contact = new CmsComponentDefinition("ContactForm", "Formulaire de contact", 1, "Formulaire editorial de prise de contact.");
        var footer = new CmsComponentDefinition("FooterLinks", "Liens footer", 1, "Colonnes de liens de bas de page.");

        db.CmsComponentDefinitions.AddRange(hero, steps, trusted, services, dashboard, faq, cta, contact, footer);

        var companySite = new CmsSite("company-public", "wélé entreprises", CmsSiteSurface.PublicCompany, coteDIvoire.Id, french.Id);
        companySite.Activate();
        companySite.SetHomePage("home");

        var providerSite = new CmsSite("provider-public", "wélé prestataires", CmsSiteSurface.PublicProvider, coteDIvoire.Id, french.Id);
        providerSite.Activate();
        providerSite.SetHomePage("home");

        var clientSite = new CmsSite("client-public", "wélé clients", CmsSiteSurface.PublicClient, coteDIvoire.Id, french.Id);
        clientSite.Activate();
        clientSite.SetHomePage("home");

        var companyPortal = new CmsSite("company-portal", "Portail entreprise", CmsSiteSurface.CompanyPortal, coteDIvoire.Id, french.Id);
        companyPortal.Activate();
        companyPortal.SetHomePage("dashboard");

        db.CmsSites.AddRange(companySite, providerSite, clientSite, companyPortal);

        AddSeedPage(db, companySite, french.Id, "home", "Accueil entreprises", "premium-b2b-landing", "entreprises", "wélé pour les entreprises", hero.Id, steps.Id, trusted.Id, dashboard.Id, faq.Id, contact.Id, footer.Id);
        AddSeedPage(db, providerSite, french.Id, "home", "Accueil prestataires", "landing", "prestataires", "wélé pour les prestataires", hero.Id, steps.Id, faq.Id);
        AddSeedPage(db, clientSite, french.Id, "home", "Accueil clients", "landing", "accueil", "wélé", hero.Id, services.Id, faq.Id);
        AddSeedPage(db, companyPortal, french.Id, "dashboard", "Tableau de bord entreprise", "portal-dashboard", "dashboard", "Tableau de bord", cta.Id);

        db.CmsMenus.AddRange(
            new CmsMenu(companySite.Id, "main", "Menu principal", "header"),
            new CmsMenu(companySite.Id, "footer", "Pied de page", "footer"),
            new CmsMenu(providerSite.Id, "main", "Menu principal", "header"),
            new CmsMenu(clientSite.Id, "main", "Menu principal", "header"),
            new CmsMenu(companyPortal.Id, "portal", "Navigation portail", "sidebar"));

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedCompanyEditorialContentAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        var french = await db.Languages.FirstAsync(language => language.Code == "fr", cancellationToken);
        var homePage = await db.CmsPages
            .Include(page => page.Site)
            .Include(page => page.Versions)
                .ThenInclude(version => version.Sections)
                    .ThenInclude(section => section.ComponentDefinition)
            .Include(page => page.Versions)
                .ThenInclude(version => version.Sections)
                    .ThenInclude(section => section.ContentValues)
            .Where(page => page.Site!.Code == "company-public" && page.Code == "home")
            .FirstOrDefaultAsync(cancellationToken);

        var version = homePage?.Versions.OrderByDescending(item => item.VersionNumber).FirstOrDefault();
        if (version is null)
        {
            return;
        }

        foreach (var section in version.Sections)
        {
            switch (section.ComponentDefinition?.Key)
            {
                case "HeroStandard":
                    AddCmsText(db, section, "label", CmsContentValueType.ShortText, "Plateforme partenaire", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "headline", CmsContentValueType.ShortText, "La plateforme qui fait grandir votre entreprise de services", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "subtitle", CmsContentValueType.LongText, "wélé connecte votre entreprise à des clients et vous donne les outils pour gérer vos techniciens, vos missions et vos revenus. Le tout depuis une interface unique.", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "primaryCta.label", CmsContentValueType.ShortText, "Devenir partenaire", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "primaryCta.url", CmsContentValueType.InternalLink, "register", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "secondaryCta.label", CmsContentValueType.ShortText, "Voir le fonctionnement", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "secondaryCta.url", CmsContentValueType.InternalLink, "#how", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "image.url", CmsContentValueType.Media, "images/wele-premium-hero.png", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "image.alt", CmsContentValueType.ShortText, "Equipe wélé en intervention chez un client", french.Id, replaceExisting: true);
                    AddCmsJson(db, section, "proofItems", "[\"Inscription gratuite\",\"Validation sous 48h\",\"Support partenaire 24/7\"]", french.Id, replaceExisting: true);
                    break;

                case "StepsTimeline":
                    AddCmsText(db, section, "label", CmsContentValueType.ShortText, "Comment ca marche", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "headline", CmsContentValueType.ShortText, "De l'inscription à votre première mission", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "subtitle", CmsContentValueType.LongText, "Un parcours clair en trois étapes.", french.Id, replaceExisting: true);
                    AddCmsJson(db, section, "steps", """
                    [
                      {"number":"01","label":"Compte","title":"Créez votre compte entreprise","text":"Renseignez les informations et les pièces légales et administratives de votre entreprise.","image":"images/wele-how-step-1.png"},
                      {"number":"02","label":"Validation","title":"Validation par nos équipes","text":"Nous vérifions et approuvons votre dossier sous 48h.","image":"images/wele-how-step-2.png"},
                      {"number":"03","label":"Demandes","title":"Recevez des demandes","text":"Ajoutez et gérez vos techniciens, recevez des demandes et suivez vos interventions.","image":"images/wele-how-step-3.png"}
                    ]
                    """, french.Id, replaceExisting: true);
                    break;

                case "TrustedLogos":
                    AddCmsText(db, section, "headline", CmsContentValueType.ShortText, "Tout ce qu'il faut pour développer votre activité", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "subtitle", CmsContentValueType.LongText, "Une infrastructure professionnelle conçue pour vous apporter des clients et simplifier votre gestion.", french.Id, replaceExisting: true);
                    AddCmsJson(db, section, "items", "[\"Demandes qualifiées\",\"Gestion des techniciens\",\"Suivi des missions\",\"Paiements sécurisés\",\"Visibilité locale\",\"Support partenaire 24/7\"]", french.Id, replaceExisting: true);
                    break;

                case "DashboardPreview":
                    AddCmsText(db, section, "label", CmsContentValueType.ShortText, "Le tableau de bord", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "headline", CmsContentValueType.ShortText, "Une interface unique pour piloter votre entreprise", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "subtitle", CmsContentValueType.LongText, "Demandes, équipes, missions et paiements : tout est réuni au même endroit.", french.Id, replaceExisting: true);
                    AddCmsJson(db, section, "stats", "[{\"label\":\"Demandes\",\"value\":\"12\",\"help\":\"+4 cette semaine\"},{\"label\":\"Assignées\",\"value\":\"8\",\"help\":\"Equipe mobilisée\"},{\"label\":\"Paiements\",\"value\":\"185k\",\"help\":\"XOF suivis\"}]", french.Id, replaceExisting: true);
                    AddCmsJson(db, section, "requests", "[\"Ménage à Cocody Riviera\",\"Jardinage à Marcory\",\"Nounou aux Deux Plateaux\"]", french.Id, replaceExisting: true);
                    AddCmsJson(db, section, "providers", "[\"Awa K. - Ménage\",\"Jean M. - Jardinage\",\"Fatou C. - Nounou\"]", french.Id, replaceExisting: true);
                    break;

                case "FaqAccordion":
                    AddCmsText(db, section, "label", CmsContentValueType.ShortText, "FAQ", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "headline", CmsContentValueType.ShortText, "Foire aux questions", french.Id, replaceExisting: true);
                    AddCmsJson(db, section, "questions", """
                    [
                      {"question":"Quel type de sociétés peuvent s'inscrire sur wélé ?","answer":"Les entreprises de jardinage, électricité, ménage à domicile, blanchisserie, dépannage auto, nounou, plomberie, climatisation, peinture, serrurerie, déménagement, maintenance maison et autres services de proximité peuvent rejoindre la plateforme selon le catalogue ouvert dans l'admin."},
                      {"question":"Comment sont vérifiées les entreprises sur wélé ?","answer":"Nous vérifions les informations de l'entreprise, les documents essentiels et le contact responsable avant l'activation complète."},
                      {"question":"L'inscription est-elle gratuite ?","answer":"Oui, la création de votre compte entreprise est entièrement gratuite. wélé ne prélève qu'une commission sur les missions réalisées avec succès."},
                      {"question":"Puis-je refuser une demande ?","answer":"Absolument. Vous restez libre d'accepter ou de refuser chaque demande selon votre disponibilité et votre zone d'intervention."},
                      {"question":"Qui choisit le technicien ?","answer":"C'est vous. Vous affectez le technicien de votre équipe que vous jugez le plus adapté à chaque intervention, ou vous laissez wélé s'en occuper."},
                      {"question":"Comment sont suivis les paiements ?","answer":"Les paiements sont sécurisés et versés rapidement sur votre compte après la clôture de chaque mission."},
                      {"question":"Qui gère les réclamations clients ?","answer":"wélé gère le support client de premier niveau, en coordination avec votre entreprise lorsque cela est nécessaire."},
                      {"question":"Puis-je mettre mon compte en pause ?","answer":"Oui, vous pouvez suspendre temporairement votre activité à tout moment depuis vos paramètres."},
                      {"question":"Combien de temps prend la validation ?","answer":"La validation de votre dossier est généralement réalisée sous 48h après réception de vos documents."}
                    ]
                    """, french.Id, replaceExisting: true);
                    break;

                case "ContactForm":
                    AddCmsText(db, section, "label", CmsContentValueType.ShortText, "Contact", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "headline", CmsContentValueType.ShortText, "Vous voulez en parler avant de vous inscrire ?", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "subtitle", CmsContentValueType.LongText, "Laissez vos coordonnées. Nous vous rappelons pour voir comment wélé peut aider votre entreprise.", french.Id, replaceExisting: true);
                    AddCmsJson(db, section, "tags", "[\"Abidjan\",\"Services à domicile\",\"Partenariat entreprise\"]", french.Id, replaceExisting: true);
                    break;

                case "FooterLinks":
                    AddCmsText(db, section, "brandText", CmsContentValueType.LongText, "La plateforme B2B pour connecter clients, entreprises et professionnels de confiance.", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "copyright", CmsContentValueType.ShortText, "© 2026 wélé Technologies. Tous droits réservés.", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "baseline", CmsContentValueType.ShortText, "Conçu pour l'Afrique de l'Ouest", french.Id, replaceExisting: true);
                    AddCmsJson(db, section, "columns", """
                    [
                      {"title":"Produit","links":["Plateforme","Fonctionnement","Tarifs","Sécurité","Intégrations","Changelog"]},
                      {"title":"Entreprise","links":["A propos","Blog","Carrières","Presse","Partenaires"]},
                      {"title":"Ressources","links":["Documentation","Centre d'aide","Communauté","Dashboard","Etudes de cas"]},
                      {"title":"Légal","links":["CGU","Confidentialité","Cookies","Mentions légales","Conditions partenaires"]}
                    ]
                    """, french.Id, replaceExisting: true);
                    break;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedProviderEditorialContentAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        var french = await db.Languages.FirstAsync(language => language.Code == "fr", cancellationToken);
        var homePage = await db.CmsPages
            .Include(page => page.Site)
            .Include(page => page.Versions)
                .ThenInclude(version => version.Sections)
                    .ThenInclude(section => section.ComponentDefinition)
            .Include(page => page.Versions)
                .ThenInclude(version => version.Sections)
                    .ThenInclude(section => section.ContentValues)
            .Where(page => page.Site!.Code == "provider-public" && page.Code == "home")
            .FirstOrDefaultAsync(cancellationToken);

        var version = homePage?.Versions.OrderByDescending(item => item.VersionNumber).FirstOrDefault();
        if (version is null)
        {
            return;
        }

        await EnsureCmsSectionsAsync(
            db,
            version,
            "Accueil prestataires",
            cancellationToken,
            "HeroStandard",
            "StepsTimeline",
            "TrustedLogos",
            "DashboardPreview",
            "FaqAccordion",
            "ContactForm",
            "FooterLinks");

        await db.SaveChangesAsync(cancellationToken);

        await db.Entry(version)
            .Collection(item => item.Sections)
            .Query()
            .Include(section => section.ComponentDefinition)
            .Include(section => section.ContentValues)
            .LoadAsync(cancellationToken);

        foreach (var section in version.Sections)
        {
            switch (section.ComponentDefinition?.Key)
            {
                case "HeroStandard":
                    AddCmsText(db, section, "label", CmsContentValueType.ShortText, "wélé prestataire", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "headline", CmsContentValueType.ShortText, "Rejoignez notre réseau de professionnels à Abidjan", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "subtitle", CmsContentValueType.LongText, "Recevez des demandes de clients et développez votre activité.", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "primaryCta.label", CmsContentValueType.ShortText, "Créer un compte", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "primaryCta.url", CmsContentValueType.InternalLink, "/onboarding?mode=register", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "secondaryCta.label", CmsContentValueType.ShortText, "Voir le fonctionnement", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "secondaryCta.url", CmsContentValueType.InternalLink, "#benefits", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "image.url", CmsContentValueType.Media, "images/wele-provider-hero.png", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "image.alt", CmsContentValueType.ShortText, "Prestataires de services à domicile wélé", french.Id, replaceExisting: true);
                    AddCmsJson(db, section, "proofItems", "[\"Clients à Abidjan\",\"Paiement sécurisé\",\"Planning libre\"]", french.Id, replaceExisting: true);
                    break;

                case "StepsTimeline":
                    AddCmsText(db, section, "label", CmsContentValueType.ShortText, "Fonctionnement", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "headline", CmsContentValueType.ShortText, "Trois étapes pour démarrer.", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "subtitle", CmsContentValueType.LongText, "Un parcours simple pour proposer votre profil en intérim à une entreprise partenaire.", french.Id, replaceExisting: true);
                    AddCmsJson(db, section, "steps", """
                    [
                      {"number":"01","label":"Formulaire","title":"Créez votre compte en ligne","text":"Renseignez vos informations, votre service principal et votre zone.","image":"images/wele-provider-step-1.svg"},
                      {"number":"02","label":"Entreprise","title":"Choisissez une entreprise","text":"wélé vous propose des entreprises qui acceptent les profils intérimaires dans votre domaine.","image":"images/wele-provider-step-2.svg"},
                      {"number":"03","label":"Validation","title":"L'entreprise étudie votre demande","text":"Si elle vous valide, vous pourrez recevoir des missions dans l'application mobile.","image":"images/wele-provider-step-3.svg"}
                    ]
                    """, french.Id, replaceExisting: true);
                    break;

                case "TrustedLogos":
                    AddCmsText(db, section, "headline", CmsContentValueType.ShortText, "Pourquoi rejoindre wélé ?", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "subtitle", CmsContentValueType.LongText, "Nous vous aidons à trouver des clients et développer votre activité.", french.Id, replaceExisting: true);
                    AddCmsJson(db, section, "items", "[\"Clients réguliers : recevez des demandes de clients dans votre zone. Plus besoin de chercher.\",\"Paiement sécurisé : les clients paient avant l'intervention, vous êtes payé rapidement.\",\"Liberté totale : vous choisissez vos horaires et les missions que vous acceptez. Pas d'engagement, vous gérez votre planning.\"]", french.Id, replaceExisting: true);
                    break;

                case "DashboardPreview":
                    AddCmsText(db, section, "label", CmsContentValueType.ShortText, "Application", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "headline", CmsContentValueType.ShortText, "Tout tient dans votre téléphone.", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "subtitle", CmsContentValueType.LongText, "Vos missions, vos services, vos messages et votre profil restent clairs, même avec peu de connexion.", french.Id, replaceExisting: true);
                    AddCmsJson(db, section, "stats", "[{\"label\":\"Mission\",\"value\":\"1\",\"help\":\"A traiter à la fois\"},{\"label\":\"Distance\",\"value\":\"2 km\",\"help\":\"Zone proche\"},{\"label\":\"Profil\",\"value\":\"92%\",\"help\":\"Presque complet\"}]", french.Id, replaceExisting: true);
                    AddCmsJson(db, section, "requests", "[\"Mission ménage à Cocody\",\"Demande jardinage à Marcory\",\"Rendez-vous électricité demain\"]", french.Id, replaceExisting: true);
                    AddCmsJson(db, section, "providers", "[\"Disponible maintenant\",\"Code entreprise actif\",\"Book photo à compléter\"]", french.Id, replaceExisting: true);
                    break;

                case "FaqAccordion":
                    AddCmsText(db, section, "label", CmsContentValueType.ShortText, "FAQ", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "headline", CmsContentValueType.ShortText, "Foire aux questions", french.Id, replaceExisting: true);
                    AddCmsJson(db, section, "questions", """
                    [
                      {"question":"Je peux m'inscrire sans entreprise ?","answer":"Oui. Vous créez un profil intérim. Une entreprise devra ensuite vous valider avant les missions."},
                      {"question":"A quoi sert le code entreprise ?","answer":"Il permet d'activer le profil que votre entreprise a déjà créé pour vous."},
                      {"question":"Quand vois-je le numéro du client ?","answer":"Après acceptation et confirmation de la mission, les contacts utiles deviennent visibles."},
                      {"question":"Pourquoi ajouter des photos ?","answer":"Pour certains services, un book aide l'entreprise à valider votre profil et vos prestations."}
                    ]
                    """, french.Id, replaceExisting: true);
                    break;

                case "ContactForm":
                    AddCmsText(db, section, "label", CmsContentValueType.ShortText, "Contact", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "headline", CmsContentValueType.ShortText, "Besoin d'aide pour démarrer ?", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "subtitle", CmsContentValueType.LongText, "Laissez vos coordonnées. Nous vous orientons vers le bon parcours.", french.Id, replaceExisting: true);
                    AddCmsJson(db, section, "tags", "[\"Abidjan\",\"Intérim\",\"Services à domicile\"]", french.Id, replaceExisting: true);
                    break;

                case "FooterLinks":
                    AddCmsText(db, section, "brandText", CmsContentValueType.LongText, "La plateforme qui rapproche les prestataires sérieux des entreprises de services.", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "copyright", CmsContentValueType.ShortText, "© 2026 wélé Technologies. Tous droits réservés.", french.Id, replaceExisting: true);
                    AddCmsText(db, section, "baseline", CmsContentValueType.ShortText, "Conçu pour l'Afrique de l'Ouest", french.Id, replaceExisting: true);
                    AddCmsJson(db, section, "columns", """
                    [
                      {"title":"Produit","links":["Fonctionnement","Sécurité","Support"]},
                      {"title":"Prestataire","links":["Créer un profil","Missions","Profil intérim"]},
                      {"title":"Ressources","links":["Centre d'aide","FAQ","Contact","WhatsApp"]},
                      {"title":"Légal","links":["CGU","Confidentialité","Mentions légales"]}
                    ]
                    """, french.Id, replaceExisting: true);
                    break;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureCmsSectionsAsync(
        HomeServiceDbContext db,
        CmsPageVersion version,
        string pageName,
        CancellationToken cancellationToken,
        params string[] componentKeys)
    {
        var existingKeys = version.Sections
            .Select(section => section.ComponentDefinition?.Key)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var definitions = await db.CmsComponentDefinitions
            .Where(component => componentKeys.Contains(component.Key))
            .ToDictionaryAsync(component => component.Key, component => component.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var nextPosition = version.Sections.Count == 0 ? 1 : version.Sections.Max(section => section.Position) + 1;
        foreach (var componentKey in componentKeys)
        {
            if (existingKeys.Contains(componentKey) || !definitions.TryGetValue(componentKey, out var definitionId))
            {
                continue;
            }

            db.CmsSections.Add(new CmsSection(version.Id, definitionId, $"{pageName} - {componentKey}", "main", nextPosition++));
        }
    }

    private static void AddSeedPage(
        HomeServiceDbContext db,
        CmsSite site,
        Guid languageId,
        string code,
        string internalName,
        string templateKey,
        string slug,
        string title,
        params Guid[] componentDefinitionIds)
    {
        var page = new CmsPage(site.Id, code, internalName, templateKey);
        var translation = new CmsPageTranslation(site.Id, page.Id, languageId, slug, title);
        var version = new CmsPageVersion(page.Id, 1);

        db.CmsPages.Add(page);
        db.CmsPageTranslations.Add(translation);
        db.CmsPageVersions.Add(version);

        for (var index = 0; index < componentDefinitionIds.Length; index++)
        {
            db.CmsSections.Add(new CmsSection(
                version.Id,
                componentDefinitionIds[index],
                $"{internalName} - section {index + 1}",
                "main",
                index + 1));
        }
    }

    private static void AddCmsText(
        HomeServiceDbContext db,
        CmsSection section,
        string fieldKey,
        CmsContentValueType valueType,
        string value,
        Guid languageId,
        bool replaceExisting = false)
    {
        var existing = section.ContentValues.FirstOrDefault(item => item.FieldKey == fieldKey && item.LanguageId == languageId);
        if (existing is not null)
        {
            if (replaceExisting)
            {
                existing.SetText(value);
            }

            return;
        }

        var contentValue = new CmsContentValue(section.Id, fieldKey, valueType, languageId);
        contentValue.SetText(value);
        db.CmsContentValues.Add(contentValue);
    }

    private static void AddCmsJson(
        HomeServiceDbContext db,
        CmsSection section,
        string fieldKey,
        string value,
        Guid languageId,
        bool replaceExisting = false)
    {
        var existing = section.ContentValues.FirstOrDefault(item => item.FieldKey == fieldKey && item.LanguageId == languageId);
        if (existing is not null)
        {
            if (replaceExisting)
            {
                existing.SetJson(value);
            }

            return;
        }

        var contentValue = new CmsContentValue(section.Id, fieldKey, CmsContentValueType.Json, languageId);
        contentValue.SetJson(value);
        db.CmsContentValues.Add(contentValue);
    }

    private sealed record TranslationSeed(string Key, string Scope, string Description, string Value);
}
