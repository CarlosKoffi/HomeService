using HomeService.Application.Admin;
using HomeService.Application.Notifications;
using HomeService.Application.Security;
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
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        await db.Database.MigrateAsync(cancellationToken);
        await EnsureMissionNumbersAsync(db, cancellationToken);
        await EnsureServiceMediaSchemaAsync(db, cancellationToken);
        await EnsureProviderServiceSchemaAsync(db, cancellationToken);
        await EnsureNotificationOutboxSchemaAsync(db, cancellationToken);
        await EnsureNotificationDeliveryRulesAsync(db, cancellationToken);
        await scope.ServiceProvider.GetRequiredService<NotificationCatalogSeeder>().EnsureDefaultsAsync(cancellationToken);
        await EnsureDefaultCommissionRulesAsync(db, cancellationToken);
        await EnsureCompanyCommissionTiersAsync(db, cancellationToken);
        await EnsureMissionWorkflowSettingsAsync(db, cancellationToken);
        await NormalizeCatalogNamesAsync(db, cancellationToken);
        await SeedCountriesAsync(db, cancellationToken);
        await SeedCountryBrandingAsync(db, cancellationToken);
        await SeedLanguagesAsync(db, cancellationToken);
        await SeedPaymentProvidersAsync(db, cancellationToken);
        await SeedServicesAsync(db, cancellationToken);
        await SeedServicePrestationsAsync(db, cancellationToken);
        await SeedWellbeingServiceCatalogAsync(db, cancellationToken);
        await SeedServiceOptionsAsync(db, cancellationToken);
        await SeedServicePrestationPhotosAsync(db, cancellationToken);
        await SeedServiceMediaAsync(db, cancellationToken);
        await EnsureQualityFoundationAsync(db, cancellationToken);
        if (configuration.GetValue<bool>("SeedData:DemoMissionsEnabled"))
        {
            await SeedDemoMissionsAsync(db, cancellationToken);
        }

        await EnableCompanyProvidersForTestingAsync(db, cancellationToken);
        await SeedAdminAccessAsync(db, configuration, cancellationToken);
        await SeedTranslationsAsync(db, cancellationToken);
        await SeedCmsFoundationAsync(db, cancellationToken);
        await EnsureClientCmsFoundationAsync(db, cancellationToken);
        await SeedClientEditorialContentAsync(db, cancellationToken);
        await SeedCompanyEditorialContentAsync(db, cancellationToken);
        await SeedProviderEditorialContentAsync(db, cancellationToken);
        await ApplyVisibleRebrandAsync(db, cancellationToken);
    }

    private static async Task SeedPaymentProvidersAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        var seeds = new[]
        {
            new PaymentProvider("orange-money", "Orange Money", PaymentMethod.MobileMoney, "Paiement depuis votre compte Orange Money.", "/media/payment-providers/orange-money.png", 10),
            new PaymentProvider("mtn-momo", "MTN MoMo", PaymentMethod.MobileMoney, "Paiement depuis votre compte MTN MoMo.", "/media/payment-providers/mtn-momo.png", 20),
            new PaymentProvider("moov-money", "Moov Money", PaymentMethod.MobileMoney, "Paiement depuis votre compte Moov Money.", "/media/payment-providers/moov-money.png", 30),
            new PaymentProvider("wave", "Wave", PaymentMethod.MobileMoney, "Paiement depuis votre compte Wave.", "/media/payment-providers/wave.png", 40),
            new PaymentProvider("djamo", "Djamo", PaymentMethod.MobileMoney, "Paiement depuis votre compte Djamo.", null, 50),
            new PaymentProvider("bank-card", "Carte bancaire", PaymentMethod.Card, "Paiement securise par carte bancaire.", "/media/payment-providers/bank-card.png", 60)
        };

        var existing = await db.PaymentProviders.ToDictionaryAsync(item => item.Code, cancellationToken);
        foreach (var seed in seeds)
        {
            if (existing.TryGetValue(seed.Code, out var provider))
            {
                provider.Update(seed.Code, seed.Name, seed.Method, seed.Description, seed.LogoUrl, seed.SortOrder);
                provider.SetActive(true);
            }
            else
            {
                db.PaymentProviders.Add(seed);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedDemoMissionsAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        await ExecuteSqlScriptAsync(db, "056_seed_demo_missions.sql", cancellationToken);
    }

    private static async Task EnsureQualityFoundationAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        var existingTemplatePrestations = await db.QualityChecklistTemplates
            .Where(item => item.ServicePrestationId != null)
            .Select(item => item.ServicePrestationId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        var prestations = await db.ServicePrestations.AsNoTracking()
            .Where(item => item.IsActive && !existingTemplatePrestations.Contains(item.Id))
            .Select(item => new { item.Id, item.ServiceId, item.Name })
            .ToListAsync(cancellationToken);

        foreach (var prestation in prestations)
        {
            var template = new QualityChecklistTemplate(
                prestation.ServiceId,
                prestation.Id,
                $"Controle qualite - {prestation.Name}",
                "Checklist operationnelle de depart, d'execution et de controle final.");
            db.QualityChecklistTemplates.Add(template);
            var items = new[]
            {
                new QualityChecklistItem(template.Id, "payment-confirmed", "Paiement de la mission confirme", QualityChecklistStage.BeforeStart, QualityChecklistResponseType.Automatic, true, 10),
                new QualityChecklistItem(template.Id, "arrival-verified", "Arrivee sur place verifiee", QualityChecklistStage.BeforeStart, QualityChecklistResponseType.Automatic, true, 20),
                new QualityChecklistItem(template.Id, "need-confirmed", "Besoin confirme avec le client", QualityChecklistStage.BeforeStart, QualityChecklistResponseType.Confirmation, true, 30, "Validez le resultat attendu avant de commencer."),
                new QualityChecklistItem(template.Id, "initial-photo", "Photo de l'etat initial", QualityChecklistStage.BeforeStart, QualityChecklistResponseType.Photo, true, 40, "Cadrez la zone concernee avant intervention."),
                new QualityChecklistItem(template.Id, "intervention-completed", "Intervention realisee selon la demande", QualityChecklistStage.DuringMission, QualityChecklistResponseType.Confirmation, true, 50),
                new QualityChecklistItem(template.Id, "result-verified", "Resultat controle et fonctionnel", QualityChecklistStage.BeforeCompletion, QualityChecklistResponseType.YesNo, true, 60, requiresEvidenceOnIssue: true),
                new QualityChecklistItem(template.Id, "area-cleaned", "Zone de travail nettoyee", QualityChecklistStage.BeforeCompletion, QualityChecklistResponseType.Confirmation, true, 70),
                new QualityChecklistItem(template.Id, "final-photo", "Photo du resultat final", QualityChecklistStage.BeforeCompletion, QualityChecklistResponseType.Photo, true, 80, "Montrez clairement le resultat livre au client.")
            };
            db.QualityChecklistItems.AddRange(items);
        }

        var existingQualifications = await db.ProviderPrestationQualifications
            .Select(item => new { item.ProviderId, item.ServicePrestationId })
            .ToListAsync(cancellationToken);
        var existingKeys = existingQualifications.Select(item => (item.ProviderId, item.ServicePrestationId)).ToHashSet();
        var providerPrestations = await (from link in db.ProviderServicePrestations.AsNoTracking()
                                         join providerService in db.ProviderServices.AsNoTracking() on link.ProviderServiceId equals providerService.Id
                                         join provider in db.Providers.AsNoTracking() on providerService.ProviderId equals provider.Id
                                         where link.IsActive && providerService.IsActive
                                         select new { provider.Id, link.ServicePrestationId, provider.Status }).Distinct().ToListAsync(cancellationToken);
        foreach (var item in providerPrestations.Where(item => !existingKeys.Contains((item.Id, item.ServicePrestationId))))
        {
            var qualification = new ProviderPrestationQualification(item.Id, item.ServicePrestationId);
            if (item.Status == ProviderStatus.Approved)
                qualification.Review(ProviderQualificationStatus.Approved, null, null, "Qualification existante reprise lors de l'activation du dispositif qualite.", null, null);
            db.ProviderPrestationQualifications.Add(qualification);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedWellbeingServiceCatalogAsync(
        HomeServiceDbContext db,
        CancellationToken cancellationToken)
    {
        await ExecuteSqlScriptAsync(db, "063_seed_wellbeing_service_catalog.sql", cancellationToken);
    }

    private static async Task SeedServicePrestationPhotosAsync(
        HomeServiceDbContext db,
        CancellationToken cancellationToken)
    {
        await ExecuteSqlScriptAsync(db, "064_seed_service_prestation_photos.sql", cancellationToken);
    }

    private static async Task EnableCompanyProvidersForTestingAsync(
        HomeServiceDbContext db,
        CancellationToken cancellationToken)
    {
        await ExecuteSqlScriptAsync(db, "065_enable_company_providers_for_testing.sql", cancellationToken);
    }

    private static async Task ExecuteSqlScriptAsync(
        HomeServiceDbContext db,
        string fileName,
        CancellationToken cancellationToken)
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "Sql", fileName);
        if (!File.Exists(scriptPath))
        {
            return;
        }

        var script = await File.ReadAllTextAsync(scriptPath, cancellationToken);
        if (!string.IsNullOrWhiteSpace(script))
        {
            await db.Database.ExecuteSqlRawAsync(script, cancellationToken);

            // Raw seed scripts can update rows already loaded by EF. Keeping those
            // snapshots would make the next SaveChanges use stale concurrency tokens.
            db.ChangeTracker.Clear();
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

    private static async Task EnsureServiceMediaSchemaAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "Services"
                ADD COLUMN IF NOT EXISTS "IconUrl" character varying(600) NULL,
                ADD COLUMN IF NOT EXISTS "ImageUrl" character varying(600) NULL;
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

    private static async Task EnsureNotificationOutboxSchemaAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "NotificationOutboxMessages"
                ADD COLUMN IF NOT EXISTS "OwnerType" character varying(32) NULL,
                ADD COLUMN IF NOT EXISTS "OwnerId" uuid NULL,
                ADD COLUMN IF NOT EXISTS "ReadAt" timestamp with time zone NULL;

            CREATE INDEX IF NOT EXISTS "IX_NotificationOutboxMessages_OwnerType_OwnerId_ReadAt"
                ON "NotificationOutboxMessages" ("OwnerType", "OwnerId", "ReadAt");
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
            SET "RateBasisPoints" = 1000,
                "UpdatedAt" = now()
            WHERE "Target" = 'CompanyRepeatCustomerOrder'
              AND "Name" = 'Commission entreprise - commande recurrente'
              AND "RateBasisPoints" = 900
              AND "FixedAmount" = 0
              AND "ServiceId" IS NULL
              AND "ServicePrestationId" IS NULL
              AND "CompanyId" IS NULL
              AND "AssignmentSource" IS NULL;

            UPDATE "CommissionRules"
            SET "RateBasisPoints" = 750,
                "UpdatedAt" = now()
            WHERE "Target" = 'CustomerServiceFee'
              AND "Name" = 'Frais de service client'
              AND "RateBasisPoints" = 400
              AND "FixedAmount" = 0
              AND "ServiceId" IS NULL
              AND "ServicePrestationId" IS NULL
              AND "CompanyId" IS NULL
              AND "AssignmentSource" IS NULL;

            INSERT INTO "CommissionRules"
                ("Id", "Name", "Target", "ServiceId", "ServicePrestationId", "CompanyId", "AssignmentSource",
                 "RateBasisPoints", "FixedAmount", "Currency", "EffectiveFrom", "EffectiveUntil", "IsActive",
                 "CreatedAt", "UpdatedAt")
            SELECT gen_random_uuid(), 'Commission entreprise - premiere commande client', 'CompanyFirstCustomerOrder',
                   NULL, NULL, NULL, NULL, 1200, 0, 'XOF', now(), NULL, true, now(), now()
            WHERE NOT EXISTS (
                SELECT 1
                FROM "CommissionRules"
                WHERE "Target" = 'CompanyFirstCustomerOrder'
                  AND "ServiceId" IS NULL
                  AND "ServicePrestationId" IS NULL
                  AND "CompanyId" IS NULL
                  AND "AssignmentSource" IS NULL
            );

            INSERT INTO "CommissionRules"
                ("Id", "Name", "Target", "ServiceId", "ServicePrestationId", "CompanyId", "AssignmentSource",
                 "RateBasisPoints", "FixedAmount", "Currency", "EffectiveFrom", "EffectiveUntil", "IsActive",
                 "CreatedAt", "UpdatedAt")
            SELECT gen_random_uuid(), 'Commission entreprise - commande recurrente', 'CompanyRepeatCustomerOrder',
                   NULL, NULL, NULL, NULL, 1000, 0, 'XOF', now(), NULL, true, now(), now()
            WHERE NOT EXISTS (
                SELECT 1
                FROM "CommissionRules"
                WHERE "Target" = 'CompanyRepeatCustomerOrder'
                  AND "ServiceId" IS NULL
                  AND "ServicePrestationId" IS NULL
                  AND "CompanyId" IS NULL
                  AND "AssignmentSource" IS NULL
            );

            INSERT INTO "CommissionRules"
                ("Id", "Name", "Target", "ServiceId", "ServicePrestationId", "CompanyId", "AssignmentSource",
                 "RateBasisPoints", "FixedAmount", "Currency", "EffectiveFrom", "EffectiveUntil", "IsActive",
                 "CreatedAt", "UpdatedAt")
            SELECT gen_random_uuid(), 'Frais de service client', 'CustomerServiceFee',
                   NULL, NULL, NULL, NULL, 750, 0, 'XOF', now(), NULL, true, now(), now()
            WHERE NOT EXISTS (
                SELECT 1
                FROM "CommissionRules"
                WHERE "Target" = 'CustomerServiceFee'
                  AND "ServiceId" IS NULL
                  AND "ServicePrestationId" IS NULL
                  AND "CompanyId" IS NULL
                  AND "AssignmentSource" IS NULL
            );
            """, cancellationToken);
    }

    private static async Task EnsureCompanyCommissionTiersAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "CompanyCommissionTiers" (
                "Id" uuid NOT NULL,
                "Name" character varying(120) NOT NULL,
                "MinimumMissionCount" integer NOT NULL,
                "RateBasisPoints" integer NOT NULL,
                "SortOrder" integer NOT NULL,
                "IsActive" boolean NOT NULL DEFAULT true,
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_CompanyCommissionTiers" PRIMARY KEY ("Id")
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_CompanyCommissionTiers_MinimumMissionCount"
                ON "CompanyCommissionTiers" ("MinimumMissionCount");
            CREATE INDEX IF NOT EXISTS "IX_CompanyCommissionTiers_IsActive_SortOrder"
                ON "CompanyCommissionTiers" ("IsActive", "SortOrder");

            ALTER TABLE "Companies"
                ADD COLUMN IF NOT EXISTS "CurrentCommissionTierName" character varying(120) NOT NULL DEFAULT 'Lancement',
                ADD COLUMN IF NOT EXISTS "CurrentCommissionTierMinimumMissionCount" integer NOT NULL DEFAULT 1,
                ADD COLUMN IF NOT EXISTS "CurrentCommissionRateBasisPoints" integer NOT NULL DEFAULT 1500;

            ALTER TABLE "Missions"
                ADD COLUMN IF NOT EXISTS "CompanyCommissionTierName" character varying(120) NULL,
                ADD COLUMN IF NOT EXISTS "CompanyCommissionMissionSequence" integer NOT NULL DEFAULT 0;

            INSERT INTO "CompanyCommissionTiers"
                ("Id", "Name", "MinimumMissionCount", "RateBasisPoints", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
            VALUES
                ('9c5d9517-ec35-4e52-84a7-100000000101', 'Lancement', 1, 1500, 10, true, now(), now()),
                ('9c5d9517-ec35-4e52-84a7-100000000102', 'Palier 50', 50, 1450, 20, true, now(), now()),
                ('9c5d9517-ec35-4e52-84a7-100000000103', 'Palier 100', 100, 1400, 30, true, now(), now()),
                ('9c5d9517-ec35-4e52-84a7-100000000104', 'Palier 150', 150, 1350, 40, true, now(), now()),
                ('9c5d9517-ec35-4e52-84a7-100000000105', 'Palier 200', 200, 1300, 50, true, now(), now()),
                ('9c5d9517-ec35-4e52-84a7-100000000106', 'Palier 250', 250, 1250, 60, true, now(), now()),
                ('9c5d9517-ec35-4e52-84a7-100000000107', 'Palier 300', 300, 1200, 70, true, now(), now()),
                ('9c5d9517-ec35-4e52-84a7-100000000108', 'Palier 350', 350, 1150, 80, true, now(), now()),
                ('9c5d9517-ec35-4e52-84a7-100000000109', 'Palier 400', 400, 1100, 90, true, now(), now()),
                ('9c5d9517-ec35-4e52-84a7-100000000110', 'Palier 450', 450, 1050, 100, true, now(), now()),
                ('9c5d9517-ec35-4e52-84a7-100000000111', 'Elite', 500, 1000, 110, true, now(), now())
            ON CONFLICT ("MinimumMissionCount") DO NOTHING;
            """, cancellationToken);
    }

    private static async Task EnsureMissionWorkflowSettingsAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "Missions"
                ADD COLUMN IF NOT EXISTS "DispatchRound" integer NOT NULL DEFAULT 0;

            ALTER TABLE "ProviderMissionAssignments"
                ADD COLUMN IF NOT EXISTS "DispatchRound" integer NOT NULL DEFAULT 1;

            CREATE TABLE IF NOT EXISTS "MissionWorkflowSettings" (
                "Id" uuid NOT NULL,
                "Key" character varying(96) NOT NULL,
                "Label" character varying(180) NOT NULL,
                "Description" character varying(360) NOT NULL,
                "Unit" character varying(40) NOT NULL,
                "Value" integer NOT NULL,
                "MinimumValue" integer NOT NULL,
                "MaximumValue" integer NOT NULL,
                "SortOrder" integer NOT NULL,
                "IsActive" boolean NOT NULL DEFAULT true,
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_MissionWorkflowSettings" PRIMARY KEY ("Id")
            );

            ALTER TABLE "MissionWorkflowSettings"
                ADD COLUMN IF NOT EXISTS "Key" character varying(96) NOT NULL DEFAULT '',
                ADD COLUMN IF NOT EXISTS "Label" character varying(180) NOT NULL DEFAULT '',
                ADD COLUMN IF NOT EXISTS "Description" character varying(360) NOT NULL DEFAULT '',
                ADD COLUMN IF NOT EXISTS "Unit" character varying(40) NOT NULL DEFAULT '',
                ADD COLUMN IF NOT EXISTS "Value" integer NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS "MinimumValue" integer NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS "MaximumValue" integer NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS "SortOrder" integer NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS "IsActive" boolean NOT NULL DEFAULT true,
                ADD COLUMN IF NOT EXISTS "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
                ADD COLUMN IF NOT EXISTS "UpdatedAt" timestamp with time zone NULL;

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_MissionWorkflowSettings_Key"
                ON "MissionWorkflowSettings" ("Key");

            CREATE INDEX IF NOT EXISTS "IX_MissionWorkflowSettings_IsActive_SortOrder"
                ON "MissionWorkflowSettings" ("IsActive", "SortOrder");

            INSERT INTO "MissionWorkflowSettings"
                ("Id", "Key", "Label", "Description", "Unit", "Value", "MinimumValue", "MaximumValue", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
            VALUES
                ('4b41a1fd-6e27-4d0a-a824-8d5d91470001', 'company_offer_response_minutes', 'Reponse entreprise', 'Temps laisse a une entreprise pour analyser une demande client et confirmer son interet avant relais.', 'minutes', 10, 1, 120, 10, true, now(), now()),
                ('4b41a1fd-6e27-4d0a-a824-8d5d91470002', 'urgent_company_offer_response_minutes', 'Reponse entreprise urgente', 'Temps laisse a une entreprise sur une demande urgente avant de proposer la mission a une autre entreprise.', 'minutes', 5, 1, 60, 20, true, now(), now()),
                ('4b41a1fd-6e27-4d0a-a824-8d5d91470008', 'company_provider_assignment_minutes', 'Affectation entreprise', 'Temps laisse a l entreprise apres acceptation pour affecter un prestataire avant redistribution automatique.', 'minutes', 10, 1, 120, 30, true, now(), now()),
                ('4b41a1fd-6e27-4d0a-a824-8d5d91470003', 'provider_acceptance_minutes', 'Acceptation prestataire', 'Temps donne au prestataire pour accepter ou refuser une mission directe avant de passer a un autre prestataire.', 'minutes', 3, 1, 30, 40, true, now(), now()),
                ('4b41a1fd-6e27-4d0a-a824-8d5d91470004', 'scheduled_provider_acceptance_minutes', 'Acceptation rendez-vous', 'Temps donne au prestataire pour accepter une mission programmee ou un rendez-vous.', 'minutes', 30, 5, 240, 50, true, now(), now()),
                ('4b41a1fd-6e27-4d0a-a824-8d5d91470005', 'customer_quote_validity_minutes', 'Validite devis client', 'Temps pendant lequel le prix propose par l entreprise reste valable cote client.', 'minutes', 30, 5, 1440, 60, true, now(), now()),
                ('4b41a1fd-6e27-4d0a-a824-8d5d91470011', 'customer_completion_validation_minutes', 'Validation fin de mission', 'Temps laisse au client pour valider la fin de mission avant validation et liberation automatiques du paiement.', 'minutes', 120, 5, 10080, 65, true, now(), now()),
                ('4b41a1fd-6e27-4d0a-a824-8d5d91470006', 'arrival_tolerance_meters', 'Tolerance arrivee GPS', 'Distance maximale acceptee entre le prestataire et le lieu client pour declarer l arrivee.', 'metres', 250, 25, 2000, 70, true, now(), now()),
                ('4b41a1fd-6e27-4d0a-a824-8d5d91470007', 'mission_start_grace_minutes', 'Marge debut mission', 'Retard tolerable apres l heure prevue avant signalement dans le suivi operationnel.', 'minutes', 15, 0, 120, 80, true, now(), now()),
                ('4b41a1fd-6e27-4d0a-a824-8d5d91470009', 'urgent_missions_enabled', 'Demandes urgentes', 'Autorise le client a demander une intervention urgente. Des frais supplementaires peuvent etre appliques.', 'boolean', 0, 0, 1, 90, true, now(), now()),
                ('4b41a1fd-6e27-4d0a-a824-8d5d91470010', 'provider_reeligibility_rounds', 'Retour des prestataires', 'Nombre de tours avant de rendre de nouveau eligibles les prestataires ayant refuse ou laisse expirer cette mission.', 'tours', 4, 1, 20, 100, true, now(), now())
                ,('4b41a1fd-6e27-4d0a-a824-8d5d91470012', 'company_commission_minimum_rating_hundredths', 'Note minimale palier commission', 'Note moyenne minimale sur les 50 derniers avis pour acceder a un meilleur palier.', 'centiemes sur 5', 450, 0, 500, 110, true, now(), now())
                ,('4b41a1fd-6e27-4d0a-a824-8d5d91470013', 'company_commission_minimum_rating_count', 'Nombre minimum d avis', 'Nombre minimum d avis clients necessaire avant le premier changement de palier.', 'avis', 10, 0, 1000, 120, true, now(), now())
                ,('4b41a1fd-6e27-4d0a-a824-8d5d91470014', 'company_commission_maximum_cancellation_basis_points', 'Annulations maximales entreprise', 'Taux maximum d annulations imputables a l entreprise ou au prestataire pour progresser.', 'points de base', 500, 0, 10000, 130, true, now(), now())
                ,('4b41a1fd-6e27-4d0a-a824-8d5d91470015', 'company_commission_cancellation_lookback', 'Fenetre annulations commission', 'Nombre de missions recentes analysees pour calculer le taux d annulation qualite.', 'missions', 100, 10, 1000, 140, true, now(), now())
            ON CONFLICT ("Key") DO UPDATE
            SET "Label" = EXCLUDED."Label",
                "Description" = EXCLUDED."Description",
                "Unit" = EXCLUDED."Unit",
                "MinimumValue" = EXCLUDED."MinimumValue",
                "MaximumValue" = EXCLUDED."MaximumValue",
                "SortOrder" = EXCLUDED."SortOrder",
                "IsActive" = true,
                "UpdatedAt" = now();
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
            new SeededService("Plomberie", "Depannage de fuites, debouchage et petites installations sanitaires.", "faucet", 5000, 9000, "XOF"),
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
            service.UpdateMedia(
                string.IsNullOrWhiteSpace(service.IconUrl)
                    ? GetSeedServiceIconUrl(normalizedName)
                    : service.IconUrl,
                service.ImageUrl);
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task SeedServiceMediaAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        var services = await db.Services.ToListAsync(cancellationToken);
        foreach (var service in services)
        {
            if (!string.IsNullOrWhiteSpace(service.IconUrl))
            {
                continue;
            }

            var iconUrl = GetSeedServiceIconUrl(service.NormalizedName);
            service.UpdateMedia(iconUrl, service.ImageUrl);
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static string? GetSeedServiceIconUrl(string normalizedName)
    {
        return normalizedName switch
        {
            "menage" or "menage a domicile" or "nettoyage" => "/assets/services/menage.png",
            "jardinage" => "/assets/services/jardinage.png",
            "electricite" => "/assets/services/electricite.png",
            "blanchisserie" or "pressing" or "repassage" => "/assets/services/blanchisserie.png",
            "depannage auto" or "assistance auto" => "/assets/services/depannage-auto.png",
            "nounou" or "garde enfants" or "garde d enfant" => "/assets/services/nounou.png",
            "plomberie" => "/assets/services/plomberie.png",
            "climatisation" => "/assets/services/climatisation.png",
            "serrurerie" => "/assets/services/serrurerie.png",
            "peinture" => "/assets/services/peinture.png",
            "anti nuisibles" or "anti-nuisibles" => "/assets/services/anti-nuisibles.png",
            "electromenager" => "/assets/services/electromenager.png",
            "manucure et pedicure" => "/assets/services/manucure-pedicure.png",
            "estheticienne" => "/assets/services/estheticienne.png",
            "coiffure" => "/assets/services/coiffure.png",
            "barbier" => "/assets/services/barbier.png",
            "massage et bien etre" => "/assets/services/massage-bien-etre.png",
            "maquillage professionnel" => "/assets/services/maquillage-professionnel.png",
            _ => "/assets/services/service-generique.png"
        };
    }

    private static async Task SeedServicePrestationsAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        var services = await db.Services
            .Where(service => service.NormalizedName == "jardinage"
                || service.NormalizedName == "menage a domicile"
                || service.NormalizedName == "nounou"
                || service.NormalizedName == "electricite"
                || service.NormalizedName == "plomberie"
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
            new SeededServicePrestation("plomberie", "Deboucher un evier", "Debouchage simple d'un evier ou d'un lavabo.", 10, 6000, 10000, "XOF"),
            new SeededServicePrestation("plomberie", "Reparer une fuite", "Recherche et reparation d'une fuite accessible.", 20, 6000, 12000, "XOF"),
            new SeededServicePrestation("plomberie", "Deboucher un WC", "Debouchage simple de toilettes sans travaux lourds.", 30, 7000, 12000, "XOF"),
            new SeededServicePrestation("plomberie", "Installer un equipement sanitaire", "Pose simple de robinet, douchette ou petit equipement sanitaire.", 40, 8000, 15000, "XOF"),
            new SeededServicePrestation("plomberie", "Reparer un chauffe-eau", "Diagnostic et petite reparation d'un chauffe-eau.", 50, 10000, 18000, "XOF"),
            new SeededServicePrestation("plomberie", "Remplacer un robinet", "Depose et remplacement d'un robinet standard.", 60, 7000, 12000, "XOF"),
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

    private static async Task SeedServiceOptionsAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        var seeds = new[]
        {
            new SeededServiceOption("Menage regulier", "Studio", "Entretien courant d'un studio.", 10, 3500, 3500, true),
            new SeededServiceOption("Menage regulier", "Appartement 2 pieces", "Entretien courant d'un appartement de deux pieces.", 20, 4500, 4500, true),
            new SeededServiceOption("Menage regulier", "Appartement 3 pieces", "Entretien courant d'un appartement de trois pieces.", 30, 5500, 5500, true),
            new SeededServiceOption("Menage regulier", "Appartement 4 pieces", "Entretien courant d'un appartement de quatre pieces.", 40, 7000, 7000, true),
            new SeededServiceOption("Menage regulier", "Maison 3 pieces", "Entretien courant d'une maison de trois pieces.", 50, 7000, 7000, true),
            new SeededServiceOption("Menage regulier", "Maison 5 pieces et plus", "Entretien d'une grande maison, prix adapte a la surface.", 60, 9000, 13000, false),
            new SeededServiceOption("Grand nettoyage", "Appartement jusqu'a 3 pieces", "Nettoyage complet d'un appartement jusqu'a trois pieces.", 10, 9000, 9000, true),
            new SeededServiceOption("Grand nettoyage", "Appartement 4 pieces et plus", "Nettoyage complet d'un grand appartement.", 20, 12000, 16000, false),
            new SeededServiceOption("Grand nettoyage", "Maison", "Nettoyage complet d'une maison selon sa surface.", 30, 15000, 25000, false),
            new SeededServiceOption("Repassage", "Petit sac - jusqu'a 10 pieces", "Repassage de dix vetements courants maximum.", 10, 3000, 3000, true),
            new SeededServiceOption("Repassage", "Sac moyen - jusqu'a 20 pieces", "Repassage de vingt vetements courants maximum.", 20, 5000, 5000, true),
            new SeededServiceOption("Repassage", "Grand sac - jusqu'a 30 pieces", "Repassage de trente vetements courants maximum.", 30, 7000, 7000, true),
            new SeededServiceOption("Lavage et pliage", "Petit lot - jusqu'a 5 kg", "Lavage, sechage et pliage d'un lot de cinq kilos maximum.", 10, 3000, 3000, true),
            new SeededServiceOption("Lavage et pliage", "Lot moyen - jusqu'a 10 kg", "Lavage, sechage et pliage d'un lot de dix kilos maximum.", 20, 5000, 5000, true),
            new SeededServiceOption("Lavage et pliage", "Grand lot - jusqu'a 15 kg", "Lavage, sechage et pliage d'un lot de quinze kilos maximum.", 30, 7000, 7000, true),
            new SeededServiceOption("Tondre le gazon", "Petite surface - jusqu'a 50 m2", "Tonte et ramassage sur une petite surface.", 10, 5000, 5000, true),
            new SeededServiceOption("Tondre le gazon", "Surface moyenne - 51 a 150 m2", "Tonte et ramassage sur une surface moyenne.", 20, 7500, 7500, true),
            new SeededServiceOption("Tondre le gazon", "Grande surface - plus de 150 m2", "Tonte d'une grande surface, montant adapte apres evaluation.", 30, 10000, 18000, false),
            new SeededServiceOption("Tailler une haie", "Jusqu'a 5 metres", "Taille et ramassage pour cinq metres de haie maximum.", 10, 6000, 6000, true),
            new SeededServiceOption("Tailler une haie", "De 6 a 15 metres", "Taille et ramassage pour une haie de six a quinze metres.", 20, 9000, 9000, true),
            new SeededServiceOption("Tailler une haie", "Plus de 15 metres", "Taille d'une grande longueur de haie apres evaluation.", 30, 12000, 20000, false),
            new SeededServiceOption("Nettoyage vitres", "Jusqu'a 5 vitres", "Nettoyage de cinq vitres accessibles maximum.", 10, 3500, 3500, true),
            new SeededServiceOption("Nettoyage vitres", "De 6 a 12 vitres", "Nettoyage de six a douze vitres accessibles.", 20, 6000, 6000, true),
            new SeededServiceOption("Nettoyage vitres", "Plus de 12 vitres", "Nettoyage d'un ensemble important de vitres apres evaluation.", 30, 8000, 14000, false)
        };

        var normalizedPrestationNames = seeds
            .Select(seed => NormalizeSeedValue(seed.PrestationName))
            .Distinct()
            .ToArray();
        var prestations = await db.ServicePrestations
            .AsNoTracking()
            .Where(prestation => normalizedPrestationNames.Contains(prestation.NormalizedName))
            .Select(prestation => new { prestation.Id, prestation.NormalizedName })
            .ToListAsync(cancellationToken);

        var prestationIds = prestations.Select(prestation => prestation.Id).ToArray();
        var existingOptionKeys = await db.ServiceOptions
            .AsNoTracking()
            .Where(option => prestationIds.Contains(option.ServicePrestationId))
            .Select(option => new { option.ServicePrestationId, option.NormalizedName })
            .ToListAsync(cancellationToken);
        var existingOptionKeySet = existingOptionKeys
            .Select(option => $"{option.ServicePrestationId:N}:{option.NormalizedName}")
            .ToHashSet(StringComparer.Ordinal);

        foreach (var seed in seeds)
        {
            var prestationName = NormalizeSeedValue(seed.PrestationName);
            foreach (var prestation in prestations.Where(item => item.NormalizedName == prestationName))
            {
                var normalizedOptionName = NormalizeSeedValue(seed.Name);
                var optionKey = $"{prestation.Id:N}:{normalizedOptionName}";
                if (!existingOptionKeySet.Add(optionKey))
                {
                    continue;
                }

                db.ServiceOptions.Add(new ServiceOption(
                    prestation.Id,
                    seed.Name,
                    seed.Description,
                    seed.SortOrder,
                    seed.PriceMinAmount,
                    seed.PriceMaxAmount,
                    seed.IsFixedPrice,
                    "XOF"));
            }
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

    private sealed record SeededServiceOption(
        string PrestationName,
        string Name,
        string Description,
        int SortOrder,
        int PriceMinAmount,
        int PriceMaxAmount,
        bool IsFixedPrice);

    private static async Task SeedAdminAccessAsync(
        HomeServiceDbContext db,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var moduleSeeds = new[]
        {
            new AdminModuleSeed(AdminModuleKey.Dashboard, "Tableau de bord", "Vue de synthese du back-office.", 10),
            new AdminModuleSeed(AdminModuleKey.CompanyApplications, "Demandes entreprises", "Validation des inscriptions, documents et activation des entreprises.", 20),
            new AdminModuleSeed(AdminModuleKey.CompanyManagement, "Entreprises", "Suivi des entreprises, prestataires, documents, missions et notifications.", 30),
            new AdminModuleSeed(AdminModuleKey.Clients, "Clients", "Consultation des profils clients, adresses, paiements, missions et documents.", 35),
            new AdminModuleSeed(AdminModuleKey.ProviderReview, "Prestataires", "Consultation, validation et suspension des prestataires.", 40),
            new AdminModuleSeed(AdminModuleKey.Services, "Services et prestations", "Gestion du catalogue, des propositions entreprises et des chiffres par service.", 50),
            new AdminModuleSeed(AdminModuleKey.Missions, "Missions", "Suivi des missions, affectations, litiges, annulations et journal.", 60),
            new AdminModuleSeed(AdminModuleKey.MissionSettings, "Parametres missions", "Configuration des commissions, delais et regles operationnelles des missions.", 70),
            new AdminModuleSeed(AdminModuleKey.Payments, "Encaissements", "Pilotage des paiements, commissions et reversements.", 80),
            new AdminModuleSeed(AdminModuleKey.Notifications, "Notifications", "Suivi des messages portail, application, email, WhatsApp et modeles.", 90),
            new AdminModuleSeed(AdminModuleKey.Cms, "CMS", "Edition des contenus, images et textes des sites publics.", 100),
            new AdminModuleSeed(AdminModuleKey.Localization, "Traductions", "Gestion des langues et textes traduisibles.", 110),
            new AdminModuleSeed(AdminModuleKey.ContactRequests, "Demandes contact", "Traitement des formulaires de contact publics.", 120),
            new AdminModuleSeed(AdminModuleKey.Audit, "Journal", "Consultation des actions sensibles et traces metier.", 130),
            new AdminModuleSeed(AdminModuleKey.AdminAccess, "Acces et roles", "Gestion des roles, modules et permissions admin.", 140)
        };

        var existingModuleKeys = await db.AdminModules
            .Select(module => module.Key)
            .ToListAsync(cancellationToken);

        var existingModuleKeySet = existingModuleKeys.ToHashSet();
        foreach (var seed in moduleSeeds)
        {
            if (!existingModuleKeySet.Contains(seed.Key))
            {
                db.AdminModules.Add(new AdminModule(seed.Key, seed.Name, seed.Description, seed.DisplayOrder));
            }
        }

        if (db.ChangeTracker.HasChanges())
        {
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

        await EnsureSuperAdminRolePermissionsAsync(db, cancellationToken);
        await EnsureFinancialAdminRolePermissionsAsync(db, cancellationToken);

        await EnsureBootstrapSuperAdminAsync(db, configuration, cancellationToken);
    }

    private static async Task EnsureSuperAdminRolePermissionsAsync(
        HomeServiceDbContext db,
        CancellationToken cancellationToken)
    {
        var superAdminRole = await db.AdminRoles
            .FirstOrDefaultAsync(role => role.Name == "Super admin", cancellationToken);
        if (superAdminRole is null)
        {
            return;
        }

        var moduleIds = await db.AdminModules
            .Select(module => module.Id)
            .ToListAsync(cancellationToken);
        var existingPermissions = await db.AdminRolePermissions
            .Where(permission => permission.RoleId == superAdminRole.Id)
            .Select(permission => new { permission.ModuleId, permission.Action })
            .ToListAsync(cancellationToken);
        var existingKeys = existingPermissions
            .Select(permission => (permission.ModuleId, permission.Action))
            .ToHashSet();

        foreach (var moduleId in moduleIds)
        {
            foreach (var action in Enum.GetValues<AdminPermissionAction>())
            {
                if (existingKeys.Add((moduleId, action)))
                {
                    db.AdminRolePermissions.Add(new AdminRolePermission(superAdminRole.Id, moduleId, action));
                }
            }
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task EnsureFinancialAdminRolePermissionsAsync(
        HomeServiceDbContext db,
        CancellationToken cancellationToken)
    {
        const string roleName = "Administration financière";
        var role = await db.AdminRoles.FirstOrDefaultAsync(item => item.Name == roleName, cancellationToken);
        if (role is null)
        {
            role = new AdminRole(
                roleName,
                "Contrôle des encaissements, remboursements, commissions et reversements entreprises.");
            db.AdminRoles.Add(role);
            await db.SaveChangesAsync(cancellationToken);
        }

        var allowed = new Dictionary<AdminModuleKey, AdminPermissionAction[]>
        {
            [AdminModuleKey.Dashboard] = [AdminPermissionAction.View],
            [AdminModuleKey.Payments] =
            [
                AdminPermissionAction.View,
                AdminPermissionAction.Create,
                AdminPermissionAction.Edit,
                AdminPermissionAction.Approve,
                AdminPermissionAction.Reject,
                AdminPermissionAction.Export
            ],
            [AdminModuleKey.Missions] =
            [
                AdminPermissionAction.View,
                AdminPermissionAction.Edit,
                AdminPermissionAction.Approve,
                AdminPermissionAction.Reject
            ],
            [AdminModuleKey.MissionSettings] =
            [
                AdminPermissionAction.View,
                AdminPermissionAction.Edit,
                AdminPermissionAction.Approve
            ],
            [AdminModuleKey.Audit] = [AdminPermissionAction.View]
        };

        var modules = await db.AdminModules
            .Where(item => allowed.Keys.Contains(item.Key))
            .ToDictionaryAsync(item => item.Key, item => item.Id, cancellationToken);
        var existing = await db.AdminRolePermissions
            .Where(item => item.RoleId == role.Id)
            .Select(item => new { item.ModuleId, item.Action })
            .ToListAsync(cancellationToken);
        var existingKeys = existing.Select(item => (item.ModuleId, item.Action)).ToHashSet();

        foreach (var (moduleKey, actions) in allowed)
        {
            if (!modules.TryGetValue(moduleKey, out var moduleId))
            {
                continue;
            }

            foreach (var action in actions)
            {
                if (existingKeys.Add((moduleId, action)))
                {
                    db.AdminRolePermissions.Add(new AdminRolePermission(role.Id, moduleId, action));
                }
            }
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task EnsureBootstrapSuperAdminAsync(
        HomeServiceDbContext db,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var email = (configuration["AdminBootstrap:Email"] ?? configuration["ADMIN_BOOTSTRAP_EMAIL"])?.Trim().ToLowerInvariant();
        var password = configuration["AdminBootstrap:Password"] ?? configuration["ADMIN_BOOTSTRAP_PASSWORD"];
        var fullName = (configuration["AdminBootstrap:FullName"] ?? configuration["ADMIN_BOOTSTRAP_FULL_NAME"] ?? "Super admin").Trim();
        var forcePasswordReset = bool.TryParse(
            configuration["AdminBootstrap:ForcePasswordReset"] ?? configuration["ADMIN_BOOTSTRAP_FORCE_PASSWORD_RESET"],
            out var configuredForcePasswordReset)
            && configuredForcePasswordReset;

        if (string.IsNullOrWhiteSpace(email)
            || !email.Contains('@', StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(password)
            || password.Length < 8)
        {
            return;
        }

        var admin = await db.AdminUsers.FirstOrDefaultAsync(user => user.Email == email, cancellationToken);
        if (admin is null)
        {
            admin = new AdminUser(fullName, email, true);
            db.AdminUsers.Add(admin);
        }
        else
        {
            admin.PromoteToSuperAdmin();
        }

        if (AdminBootstrapPasswordPolicy.ShouldSetPassword(admin.PasswordHash, forcePasswordReset))
        {
            admin.AcceptInvitation(Sha256PasswordHasher.Hash(password), DateTimeOffset.UtcNow);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed record AdminModuleSeed(AdminModuleKey Key, string Name, string Description, int DisplayOrder);

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

    private static async Task EnsureClientCmsFoundationAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        var french = await db.Languages.FirstAsync(language => language.Code == "fr", cancellationToken);
        var coteDIvoire = await db.Countries.FirstAsync(country => country.IsoCode == "CI", cancellationToken);
        var site = await db.CmsSites.FirstOrDefaultAsync(item => item.Code == "client-public", cancellationToken);

        if (site is null)
        {
            site = new CmsSite("client-public", "Wélé clients", CmsSiteSurface.PublicClient, coteDIvoire.Id, french.Id);
            site.Activate();
            site.SetHomePage("home");
            db.CmsSites.Add(site);
            await db.SaveChangesAsync(cancellationToken);
        }

        var requiredDefinitions = new[]
        {
            new ComponentDefinitionSeed("HeroStandard", "Hero standard", "Section d'ouverture avec titre, texte et appels à l'action."),
            new ComponentDefinitionSeed("ServicesList", "Liste de services", "Présentation éditoriale du catalogue de services."),
            new ComponentDefinitionSeed("StepsTimeline", "Parcours en étapes", "Explication du parcours client."),
            new ComponentDefinitionSeed("TrustedLogos", "Bloc confiance", "Arguments de confiance et média associé."),
            new ComponentDefinitionSeed("FaqAccordion", "Bloc de recommandation", "Contenu de commande récurrente."),
            new ComponentDefinitionSeed("DashboardPreview", "Aperçu application", "Présentation de l'application mobile."),
            new ComponentDefinitionSeed("ContactForm", "Passerelles Wélé", "Liens vers les parcours prestataire et entreprise."),
            new ComponentDefinitionSeed("FooterLinks", "Liens footer", "Colonnes de liens de bas de page.")
        };
        var existingDefinitions = await db.CmsComponentDefinitions
            .Where(component => requiredDefinitions.Select(seed => seed.Key).Contains(component.Key))
            .ToDictionaryAsync(component => component.Key, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var definition in requiredDefinitions.Where(definition => !existingDefinitions.ContainsKey(definition.Key)))
        {
            var component = new CmsComponentDefinition(definition.Key, definition.Name, 1, definition.Description);
            db.CmsComponentDefinitions.Add(component);
            existingDefinitions[definition.Key] = component;
        }

        await db.SaveChangesAsync(cancellationToken);

        var pageExists = await db.CmsPages.AnyAsync(
            page => page.SiteId == site.Id && page.Code == "home",
            cancellationToken);
        if (!pageExists)
        {
            AddSeedPage(
                db,
                site,
                french.Id,
                "home",
                "Accueil clients",
                "landing",
                "accueil",
                "Wélé",
                requiredDefinitions.Select(definition => existingDefinitions[definition.Key].Id).ToArray());
        }

        var menuExists = await db.CmsMenus.AnyAsync(
            menu => menu.SiteId == site.Id && menu.Code == "main",
            cancellationToken);
        if (!menuExists)
        {
            db.CmsMenus.Add(new CmsMenu(site.Id, "main", "Menu principal", "header"));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedClientEditorialContentAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
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
            .Where(page => page.Site!.Code == "client-public" && page.Code == "home")
            .FirstOrDefaultAsync(cancellationToken);

        var version = homePage?.Versions.OrderByDescending(item => item.VersionNumber).FirstOrDefault();
        if (version is null)
        {
            return;
        }

        await EnsureCmsSectionsAsync(
            db,
            version,
            "Accueil clients",
            cancellationToken,
            "HeroStandard",
            "ServicesList",
            "StepsTimeline",
            "TrustedLogos",
            "FaqAccordion",
            "DashboardPreview",
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
                    AddCmsText(db, section, "label", CmsContentValueType.ShortText, "Abidjan, Côte d’Ivoire", french.Id);
                    AddCmsText(db, section, "headline", CmsContentValueType.ShortText, "Le bon service, au bon moment.", french.Id);
                    AddCmsText(db, section, "subtitle", CmsContentValueType.LongText, "Des professionnels vérifiés pour votre maison, votre bien-être et votre quotidien.", french.Id);
                    AddCmsText(db, section, "primaryCta.label", CmsContentValueType.ShortText, "Commander un service", french.Id);
                    AddCmsText(db, section, "primaryCta.url", CmsContentValueType.InternalLink, "#commander", french.Id);
                    AddCmsText(db, section, "secondaryCta.label", CmsContentValueType.ShortText, "Découvrir Wélé", french.Id);
                    AddCmsText(db, section, "secondaryCta.url", CmsContentValueType.InternalLink, "#services", french.Id);
                    AddCmsText(db, section, "image.url", CmsContentValueType.Media, "website/client/wele-client-hero.png", french.Id);
                    AddCmsText(db, section, "image.alt", CmsContentValueType.ShortText, "Une cliente accueille une professionnelle Wélé à Abidjan", french.Id);
                    AddCmsJson(db, section, "proofItems", "[\"Professionnels vérifiés et notés\",\"Paiement sécurisé\",\"Service suivi en direct\",\"Assistance réactive\"]", french.Id);
                    break;

                case "ServicesList":
                    AddCmsText(db, section, "label", CmsContentValueType.ShortText, "Des services pour chaque besoin", french.Id);
                    AddCmsText(db, section, "headline", CmsContentValueType.ShortText, "Tout ce que Wélé peut faire pour vous.", french.Id);
                    AddCmsText(db, section, "subtitle", CmsContentValueType.LongText, "Choisissez un univers, puis retrouvez les prestations réellement disponibles dans votre zone.", french.Id);
                    break;

                case "StepsTimeline":
                    AddCmsText(db, section, "label", CmsContentValueType.ShortText, "Simple, clair, suivi", french.Id);
                    AddCmsText(db, section, "headline", CmsContentValueType.ShortText, "Commandez en toute confiance.", french.Id);
                    AddCmsText(db, section, "subtitle", CmsContentValueType.LongText, "Une demande claire, un professionnel compatible et un suivi jusqu’à la fin.", french.Id);
                    AddCmsJson(db, section, "steps", """
                    [
                      {"number":"01","label":"Service","title":"Choisissez votre service","text":"Décrivez votre besoin, ajoutez l’adresse et choisissez maintenant ou sur rendez-vous.","image":""},
                      {"number":"02","label":"Professionnel","title":"Un professionnel vérifié accepte","text":"Nous cherchons l’entreprise et le professionnel compatibles avec votre demande.","image":""},
                      {"number":"03","label":"Suivi","title":"Suivez, payez et évaluez","text":"Recevez les étapes importantes, payez au bon moment et partagez votre avis.","image":""}
                    ]
                    """, french.Id);
                    break;

                case "TrustedLogos":
                    AddCmsText(db, section, "label", CmsContentValueType.ShortText, "Pensé pour la confiance", french.Id);
                    AddCmsText(db, section, "headline", CmsContentValueType.ShortText, "Votre quotidien mérite des professionnels fiables.", french.Id);
                    AddCmsText(db, section, "subtitle", CmsContentValueType.LongText, "Chaque mission est encadrée : identité et compétences vérifiées, paiement au bon moment, suivi des étapes et avis après intervention.", french.Id);
                    AddCmsText(db, section, "primaryCta.label", CmsContentValueType.ShortText, "Découvrir l’application", french.Id);
                    AddCmsText(db, section, "primaryCta.url", CmsContentValueType.InternalLink, "#telecharger", french.Id);
                    AddCmsText(db, section, "image.url", CmsContentValueType.Media, "website/client/wele-trust-team.png", french.Id);
                    AddCmsText(db, section, "image.alt", CmsContentValueType.ShortText, "Équipe de professionnels Wélé", french.Id);
                    AddCmsJson(db, section, "items", "[\"Identité contrôlée\",\"Compétences validées\",\"Avis authentiques\"]", french.Id);
                    break;

                case "FaqAccordion":
                    AddCmsText(db, section, "label", CmsContentValueType.ShortText, "Votre historique devient utile", french.Id);
                    AddCmsText(db, section, "headline", CmsContentValueType.ShortText, "Un service vous a plu ?", french.Id);
                    AddCmsText(db, section, "subtitle", CmsContentValueType.LongText, "Retrouvez votre entreprise favorite et relancez une demande en quelques secondes. Si elle est indisponible, Wélé continue la recherche pour vous.", french.Id);
                    AddCmsText(db, section, "primaryCta.label", CmsContentValueType.ShortText, "Commander à nouveau", french.Id);
                    AddCmsText(db, section, "primaryCta.url", CmsContentValueType.InternalLink, "#telecharger", french.Id);
                    AddCmsText(db, section, "profile.name", CmsContentValueType.ShortText, "Awa Kouamé", french.Id);
                    AddCmsText(db, section, "profile.service", CmsContentValueType.ShortText, "Ménage & repassage", french.Id);
                    AddCmsText(db, section, "profile.rating", CmsContentValueType.ShortText, "4,9 ★", french.Id);
                    AddCmsText(db, section, "profile.badge", CmsContentValueType.ShortText, "Vérifiée", french.Id);
                    AddCmsText(db, section, "profile.image.url", CmsContentValueType.Media, "images/awa-kouame-profile.webp", french.Id);
                    AddCmsText(db, section, "profile.image.alt", CmsContentValueType.ShortText, "Portrait d’Awa Kouamé", french.Id);
                    break;

                case "DashboardPreview":
                    AddCmsText(db, section, "label", CmsContentValueType.ShortText, "Tout Wélé dans votre téléphone", french.Id);
                    AddCmsText(db, section, "headline", CmsContentValueType.ShortText, "Wélé vous accompagne partout.", french.Id);
                    AddCmsText(db, section, "subtitle", CmsContentValueType.LongText, "Finalisez votre demande, recevez les notifications importantes, suivez l’arrivée du professionnel et retrouvez vos factures.", french.Id);
                    AddCmsJson(db, section, "items", "[\"Suivi de mission\",\"Notifications utiles\",\"Paiement sécurisé\",\"Messagerie encadrée\"]", french.Id);
                    break;

                case "ContactForm":
                    AddCmsText(db, section, "provider.headline", CmsContentValueType.ShortText, "Vous êtes professionnel ?", french.Id);
                    AddCmsText(db, section, "provider.subtitle", CmsContentValueType.LongText, "Recevez des missions adaptées à vos compétences.", french.Id);
                    AddCmsText(db, section, "providerCta.label", CmsContentValueType.ShortText, "Devenir prestataire", french.Id);
                    AddCmsText(db, section, "providerCta.url", CmsContentValueType.ExternalLink, "https://pro.wele.africa", french.Id);
                    AddCmsText(db, section, "company.headline", CmsContentValueType.ShortText, "Vous êtes une entreprise ?", french.Id);
                    AddCmsText(db, section, "company.subtitle", CmsContentValueType.LongText, "Développez et pilotez votre activité avec Wélé.", french.Id);
                    AddCmsText(db, section, "companyCta.label", CmsContentValueType.ShortText, "Devenir partenaire", french.Id);
                    AddCmsText(db, section, "companyCta.url", CmsContentValueType.ExternalLink, "https://entreprise.wele.africa", french.Id);
                    break;

                case "FooterLinks":
                    AddCmsText(db, section, "brandText", CmsContentValueType.LongText, "Le bon service, au bon moment.", french.Id);
                    AddCmsText(db, section, "copyright", CmsContentValueType.ShortText, "© 2026 Wélé. Tous droits réservés.", french.Id);
                    AddCmsText(db, section, "baseline", CmsContentValueType.ShortText, "Abidjan · Côte d’Ivoire", french.Id);
                    AddCmsJson(db, section, "columns", """
                    [
                      {"title":"Services","links":["Maison","Bien-être","Dépannage"]},
                      {"title":"Rejoindre Wélé","links":["Prestataires","Entreprises"]},
                      {"title":"Assistance","links":["contact@wele.africa","Confiance et sécurité"]}
                    ]
                    """, french.Id);
                    break;
            }
        }

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
            // Seed values are defaults only. Content edited from the CMS must survive
            // application restarts and future deployments.
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
            // Seed values are defaults only. Content edited from the CMS must survive
            // application restarts and future deployments.
            return;
        }

        var contentValue = new CmsContentValue(section.Id, fieldKey, CmsContentValueType.Json, languageId);
        contentValue.SetJson(value);
        db.CmsContentValues.Add(contentValue);
    }

    private sealed record ComponentDefinitionSeed(string Key, string Name, string Description);
    private sealed record TranslationSeed(string Key, string Scope, string Description, string Value);
}
