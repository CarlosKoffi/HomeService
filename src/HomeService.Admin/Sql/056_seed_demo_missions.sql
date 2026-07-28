-- Idempotent demo data for portal/admin mission and payment testing.
-- Creates up to 15 demo missions per company that already has providers.
-- Completed demo missions use Mobile Money or card payments, never cash.

CREATE TEMP TABLE IF NOT EXISTS "WeleDemoMissionSeed" (
    "MissionId" uuid NOT NULL,
    "MissionNumber" text NOT NULL,
    "CustomerId" uuid NOT NULL,
    "CompanyId" uuid NOT NULL,
    "ProviderId" uuid NOT NULL,
    "ServiceId" uuid NOT NULL,
    "ServicePrestationId" uuid NULL,
    "SlotNumber" integer NOT NULL,
    "CustomerFirstName" text NOT NULL,
    "CustomerLastName" text NOT NULL,
    "CustomerPhoneNumber" text NOT NULL,
    "Mode" text NOT NULL,
    "Status" text NOT NULL,
    "AssignmentStatus" text NOT NULL,
    "PaymentMethod" text NOT NULL,
    "PaymentStatus" text NOT NULL,
    "QuoteStatus" text NOT NULL,
    "ScheduledFor" timestamptz NULL,
    "EstimatedDurationMinutes" integer NOT NULL,
    "ActualDurationMinutes" integer NULL,
    "EstimatedTotalAmount" integer NOT NULL,
    "FinalTotalAmount" integer NULL,
    "CompanyQuotedAmount" integer NOT NULL,
    "PartsEstimateAmount" integer NULL,
    "PartsDescription" text NULL,
    "PlatformCommissionAmount" integer NOT NULL,
    "CompanyPayoutAmount" integer NOT NULL,
    "TransportFeeAmount" integer NOT NULL,
    "ServiceAddress" text NOT NULL,
    "ServiceLatitude" numeric(9,6) NULL,
    "ServiceLongitude" numeric(9,6) NULL,
    "Description" text NOT NULL,
    "CreatedAt" timestamptz NOT NULL
) ON COMMIT DROP;

TRUNCATE TABLE "WeleDemoMissionSeed";

WITH company_scope AS (
    SELECT
        company."Id" AS "CompanyId",
        COALESCE(existing."DemoCount", 0) AS "DemoCount"
    FROM "Companies" company
    LEFT JOIN LATERAL (
        SELECT COUNT(*)::integer AS "DemoCount"
        FROM "Missions" mission
        WHERE mission."CompanyId" = company."Id"
          AND mission."Description" LIKE '[seed-demo-missions]%'
    ) existing ON true
    WHERE EXISTS (
        SELECT 1
        FROM "Providers" provider
        WHERE provider."CompanyId" = company."Id"
    )
),
slots AS (
    SELECT
        scope."CompanyId",
        generate_series(scope."DemoCount" + 1, 15) AS "SlotNumber"
    FROM company_scope scope
    WHERE scope."DemoCount" < 15
),
provider_pool AS (
    SELECT
        provider."CompanyId",
        provider."Id" AS "ProviderId",
        ROW_NUMBER() OVER (
            PARTITION BY provider."CompanyId"
            ORDER BY md5(provider."Id"::text)
        ) AS "ProviderRank",
        COUNT(*) OVER (PARTITION BY provider."CompanyId") AS "ProviderCount"
    FROM "Providers" provider
    WHERE provider."CompanyId" IS NOT NULL
),
fallback_service AS (
    SELECT service."Id" AS "ServiceId"
    FROM "Services" service
    WHERE service."IsActive" = true
    ORDER BY service."Name"
    LIMIT 1
),
chosen AS (
    SELECT
        gen_random_uuid() AS "MissionId",
        gen_random_uuid() AS "CustomerId",
        slot."CompanyId",
        provider."ProviderId",
        COALESCE(provider_service."ServiceId", fallback_service."ServiceId") AS "ServiceId",
        provider_service."ServicePrestationId",
        slot."SlotNumber",
        COALESCE(provider_service."PriceMinAmount", service."PriceMinAmount", 3500) AS "PriceMinAmount",
        COALESCE(provider_service."PriceMaxAmount", service."PriceMaxAmount", service."PremiumPriceAmount", 6500) AS "PriceMaxAmount",
        service."Name" AS "ServiceName",
        prestation."Name" AS "PrestationName"
    FROM slots slot
    JOIN provider_pool provider
      ON provider."CompanyId" = slot."CompanyId"
     AND provider."ProviderRank" = ((slot."SlotNumber" - 1) % provider."ProviderCount") + 1
    CROSS JOIN fallback_service
    LEFT JOIN LATERAL (
        SELECT
            provider_service."ServiceId",
            provider_prestation."ServicePrestationId",
            COALESCE(service_prestation."PriceMinAmount", service."PriceMinAmount", 3500) AS "PriceMinAmount",
            COALESCE(service_prestation."PriceMaxAmount", service."PriceMaxAmount", 6500) AS "PriceMaxAmount"
        FROM "ProviderServices" provider_service
        JOIN "Services" service ON service."Id" = provider_service."ServiceId"
        LEFT JOIN "ProviderServicePrestations" provider_prestation
          ON provider_prestation."ProviderServiceId" = provider_service."Id"
         AND provider_prestation."IsActive" = true
        LEFT JOIN "ServicePrestations" service_prestation
          ON service_prestation."Id" = provider_prestation."ServicePrestationId"
        WHERE provider_service."ProviderId" = provider."ProviderId"
        ORDER BY provider_service."IsActive" DESC, provider_service."CreatedAt" DESC, service."Name", service_prestation."SortOrder" NULLS LAST
        LIMIT 1
    ) provider_service ON true
    JOIN "Services" service ON service."Id" = COALESCE(provider_service."ServiceId", fallback_service."ServiceId")
    LEFT JOIN "ServicePrestations" prestation ON prestation."Id" = provider_service."ServicePrestationId"
)
INSERT INTO "WeleDemoMissionSeed" (
    "MissionId",
    "MissionNumber",
    "CustomerId",
    "CompanyId",
    "ProviderId",
    "ServiceId",
    "ServicePrestationId",
    "SlotNumber",
    "CustomerFirstName",
    "CustomerLastName",
    "CustomerPhoneNumber",
    "Mode",
    "Status",
    "AssignmentStatus",
    "PaymentMethod",
    "PaymentStatus",
    "QuoteStatus",
    "ScheduledFor",
    "EstimatedDurationMinutes",
    "ActualDurationMinutes",
    "EstimatedTotalAmount",
    "FinalTotalAmount",
    "CompanyQuotedAmount",
    "PartsEstimateAmount",
    "PartsDescription",
    "PlatformCommissionAmount",
    "CompanyPayoutAmount",
    "TransportFeeAmount",
    "ServiceAddress",
    "ServiceLatitude",
    "ServiceLongitude",
    "Description",
    "CreatedAt"
)
SELECT
    chosen."MissionId",
    upper(concat('MIS-', to_char(now(), 'YYMMDD'), '-', substr(replace(chosen."MissionId"::text, '-', ''), 1, 8))),
    chosen."CustomerId",
    chosen."CompanyId",
    chosen."ProviderId",
    chosen."ServiceId",
    chosen."ServicePrestationId",
    chosen."SlotNumber",
    (ARRAY['Aminata','Fatou','Grace','Mariam','Nadege','Prisca','Awa','Carole','Estelle','Nadia','Akissi','Sonia','Rokia','Ines','Clarisse'])[((chosen."SlotNumber" - 1) % 15) + 1],
    (ARRAY['Kouame','Traore','Diallo','Bamba','Kone','Yao','Amani','Coulibaly','Nguessan','Koffi','Soro','Diaby','Toure','Konan','Kouassi'])[((chosen."SlotNumber" - 1) % 15) + 1],
    '+22507' || LPAD(((90000000 + (chosen."SlotNumber" * 317) + ABS(hashtext(chosen."CompanyId"::text)::bigint)) % 100000000)::text, 8, '0'),
    CASE WHEN chosen."SlotNumber" IN (1, 4, 7, 10, 13) THEN 'Instant' ELSE 'Scheduled' END,
    'Completed',
    'Completed',
    CASE
        WHEN chosen."SlotNumber" % 2 = 0 THEN 'Card'
        ELSE 'MobileMoney'
    END,
    'Paid',
    'Accepted',
    now() - (((chosen."SlotNumber" - 1) % 6) || ' days')::interval - ((8 + (chosen."SlotNumber" % 7)) || ' hours')::interval,
    90 + ((chosen."SlotNumber" % 4) * 30),
    95 + ((chosen."SlotNumber" % 4) * 25),
    LEAST(chosen."PriceMaxAmount", chosen."PriceMinAmount" + ((chosen."SlotNumber" % 4) * 750)) + CASE WHEN chosen."SlotNumber" % 5 = 0 THEN 3000 ELSE 0 END,
    LEAST(chosen."PriceMaxAmount", chosen."PriceMinAmount" + ((chosen."SlotNumber" % 4) * 750)) + CASE WHEN chosen."SlotNumber" % 5 = 0 THEN 3000 ELSE 0 END,
    LEAST(chosen."PriceMaxAmount", chosen."PriceMinAmount" + ((chosen."SlotNumber" % 4) * 750)) + CASE WHEN chosen."SlotNumber" % 5 = 0 THEN 3000 ELSE 0 END,
    CASE WHEN chosen."SlotNumber" % 5 = 0 THEN 3000 ELSE NULL END,
    CASE WHEN chosen."SlotNumber" % 5 = 0 THEN 'Petites fournitures incluses dans le devis.' ELSE NULL END,
    ROUND((LEAST(chosen."PriceMaxAmount", chosen."PriceMinAmount" + ((chosen."SlotNumber" % 4) * 750)) + CASE WHEN chosen."SlotNumber" % 5 = 0 THEN 3000 ELSE 0 END) * 0.15)::integer,
    (LEAST(chosen."PriceMaxAmount", chosen."PriceMinAmount" + ((chosen."SlotNumber" % 4) * 750)) + CASE WHEN chosen."SlotNumber" % 5 = 0 THEN 3000 ELSE 0 END)
        - ROUND((LEAST(chosen."PriceMaxAmount", chosen."PriceMinAmount" + ((chosen."SlotNumber" % 4) * 750)) + CASE WHEN chosen."SlotNumber" % 5 = 0 THEN 3000 ELSE 0 END) * 0.15)::integer,
    0,
    (ARRAY['Cocody Riviera 2','Marcory Zone 4','Yopougon Niangon','Plateau Centre','Bingerville Carrefour','Abobo Baoule','Treichville Avenue 8','Angre 7e tranche'])[((chosen."SlotNumber" - 1) % 8) + 1],
    (ARRAY[5.359952,5.302840,5.336220,5.319720,5.350900,5.416500,5.301100,5.395000])[((chosen."SlotNumber" - 1) % 8) + 1],
    (ARRAY[-3.973560,-3.982740,-4.092100,-4.016110,-3.887200,-4.025600,-4.007600,-3.981200])[((chosen."SlotNumber" - 1) % 8) + 1],
    '[seed-demo-missions] ' || COALESCE(chosen."PrestationName", chosen."ServiceName") || ' - demande client avec photos et devis entreprise.',
    now() - (((chosen."SlotNumber" - 1) % 6) || ' days')::interval - ((10 + (chosen."SlotNumber" % 7)) || ' hours')::interval
FROM chosen;

INSERT INTO "Customers" ("Id", "FirstName", "LastName", "PhoneNumber", "CreatedAt", "UpdatedAt")
SELECT
    seed."CustomerId",
    seed."CustomerFirstName",
    seed."CustomerLastName",
    seed."CustomerPhoneNumber",
    seed."CreatedAt",
    seed."CreatedAt" + interval '5 minutes'
FROM "WeleDemoMissionSeed" seed;

INSERT INTO "Missions" (
    "Id",
    "MissionNumber",
    "CustomerId",
    "ServiceId",
    "ServicePrestationId",
    "ProviderId",
    "CompanyId",
    "Mode",
    "Status",
    "Description",
    "RequiresCompanyQuote",
    "QuoteStatus",
    "PaymentMethod",
    "PaymentStatus",
    "ScheduledFor",
    "EstimatedDurationMinutes",
    "ActualDurationMinutes",
    "EstimatedTotalAmount",
    "FinalTotalAmount",
    "CompanyQuotedAmount",
    "CompanyQuoteJustification",
    "PartsEstimateAmount",
    "PartsDescription",
    "CompanyQuotedAt",
    "CustomerQuoteAcceptedAt",
    "PlatformCommissionAmount",
    "PlatformCommissionRateBasisPoints",
    "KazaAssignmentCommissionRateBasisPoints",
    "CompanyPayoutAmount",
    "TransportFeeAmount",
    "CancellationFeeAmount",
    "AssignmentSource",
    "IsInterimProviderSnapshot",
    "Currency",
    "ServiceAddress",
    "ServiceLatitude",
    "ServiceLongitude",
    "ArrivalToleranceMeters",
    "ProviderAcceptedAt",
    "CustomerConfirmedAt",
    "ContactDetailsReleasedAt",
    "CreatedAt",
    "UpdatedAt"
)
SELECT
    seed."MissionId",
    seed."MissionNumber",
    seed."CustomerId",
    seed."ServiceId",
    seed."ServicePrestationId",
    seed."ProviderId",
    seed."CompanyId",
    seed."Mode",
    seed."Status",
    seed."Description",
    true,
    seed."QuoteStatus",
    seed."PaymentMethod",
    seed."PaymentStatus",
    seed."ScheduledFor",
    seed."EstimatedDurationMinutes",
    seed."ActualDurationMinutes",
    seed."EstimatedTotalAmount",
    seed."FinalTotalAmount",
    seed."CompanyQuotedAmount",
    CASE WHEN seed."PartsEstimateAmount" IS NULL THEN 'Prix confirme dans la fourchette du service.' ELSE 'Devis avec fournitures separees.' END,
    seed."PartsEstimateAmount",
    seed."PartsDescription",
    seed."CreatedAt" + interval '10 minutes',
    seed."CreatedAt" + interval '25 minutes',
    seed."PlatformCommissionAmount",
    1500,
    0,
    seed."CompanyPayoutAmount",
    seed."TransportFeeAmount",
    0,
    'Company',
    EXISTS (
        SELECT 1
        FROM "Providers" provider
        WHERE provider."Id" = seed."ProviderId"
          AND provider."EmploymentType" = 'TemporaryWorker'
    ),
    'XOF',
    seed."ServiceAddress",
    seed."ServiceLatitude",
    seed."ServiceLongitude",
    250,
    seed."CreatedAt" + interval '18 minutes',
    seed."CreatedAt" + interval '28 minutes',
    seed."CreatedAt" + interval '28 minutes',
    seed."CreatedAt",
    seed."ScheduledFor" + ((seed."ActualDurationMinutes" || ' minutes')::interval)
FROM "WeleDemoMissionSeed" seed;

INSERT INTO "ProviderMissionAssignments" (
    "Id",
    "MissionId",
    "ProviderId",
    "CompanyId",
    "Status",
    "ExpiresAt",
    "RespondedAt",
    "StartedAt",
    "CompletedAt",
    "CompletionNote",
    "CompletionPhotoPath",
    "OfferedLatitude",
    "OfferedLongitude",
    "OfferedAccuracyMeters",
    "AcceptedLatitude",
    "AcceptedLongitude",
    "AcceptedAccuracyMeters",
    "ArrivalLatitude",
    "ArrivalLongitude",
    "ArrivalAccuracyMeters",
    "ArrivalDistanceMeters",
    "ArrivalToleranceMeters",
    "ArrivalVerificationStatus",
    "ArrivalVerifiedAt",
    "CreatedAt",
    "UpdatedAt"
)
SELECT
    gen_random_uuid(),
    seed."MissionId",
    seed."ProviderId",
    seed."CompanyId",
    seed."AssignmentStatus",
    seed."CreatedAt" + interval '3 minutes',
    seed."CreatedAt" + interval '2 minutes',
    seed."ScheduledFor",
    seed."ScheduledFor" + ((seed."ActualDurationMinutes" || ' minutes')::interval),
    'Mission terminee, client satisfait.',
    'demo-missions/completion-' || seed."SlotNumber" || '.jpg',
    seed."ServiceLatitude",
    seed."ServiceLongitude",
    45,
    seed."ServiceLatitude" + 0.000120,
    seed."ServiceLongitude" + 0.000120,
    35,
    seed."ServiceLatitude" + 0.000050,
    seed."ServiceLongitude" + 0.000050,
    25,
    12,
    250,
    'Verified',
    seed."ScheduledFor" - interval '5 minutes',
    seed."CreatedAt",
    seed."ScheduledFor" + ((seed."ActualDurationMinutes" || ' minutes')::interval)
FROM "WeleDemoMissionSeed" seed;

INSERT INTO "MissionAttachments" (
    "Id",
    "MissionId",
    "AttachmentType",
    "OriginalFileName",
    "StoragePath",
    "ContentType",
    "FileSizeBytes",
    "Caption",
    "IsDeleted",
    "CreatedAt",
    "UpdatedAt"
)
SELECT
    gen_random_uuid(),
    seed."MissionId",
    'CustomerPhoto',
    'photo-demande-' || seed."SlotNumber" || '.jpg',
    'demo-missions/customer-request-' || seed."SlotNumber" || '.jpg',
    'image/jpeg',
    180000 + (seed."SlotNumber" * 2048),
    'Photo client pour aider l''entreprise a evaluer la demande.',
    false,
    seed."CreatedAt" + interval '1 minute',
    seed."CreatedAt" + interval '1 minute'
FROM "WeleDemoMissionSeed" seed
WHERE seed."SlotNumber" % 2 = 0
UNION ALL
SELECT
    gen_random_uuid(),
    seed."MissionId",
    'ProviderCompletionPhoto',
    'photo-fin-mission-' || seed."SlotNumber" || '.jpg',
    'demo-missions/provider-completion-' || seed."SlotNumber" || '.jpg',
    'image/jpeg',
    210000 + (seed."SlotNumber" * 2048),
    'Photo de fin de prestation.',
    false,
    seed."ScheduledFor" + interval '2 hours',
    seed."ScheduledFor" + interval '2 hours'
FROM "WeleDemoMissionSeed" seed
WHERE seed."Status" = 'Completed';

INSERT INTO "MissionFinancialBreakdowns" (
    "Id",
    "MissionId",
    "LineType",
    "Label",
    "Amount",
    "Currency",
    "SortOrder",
    "CreatedAt",
    "UpdatedAt"
)
SELECT gen_random_uuid(), seed."MissionId", 'ServicePrice', 'Prestation', seed."CompanyQuotedAmount" - COALESCE(seed."PartsEstimateAmount", 0), 'XOF', 10, seed."CreatedAt", seed."CreatedAt"
FROM "WeleDemoMissionSeed" seed
UNION ALL
SELECT gen_random_uuid(), seed."MissionId", 'PartsEstimate', 'Fournitures', seed."PartsEstimateAmount", 'XOF', 20, seed."CreatedAt", seed."CreatedAt"
FROM "WeleDemoMissionSeed" seed
WHERE seed."PartsEstimateAmount" IS NOT NULL
UNION ALL
SELECT gen_random_uuid(), seed."MissionId", 'PlatformCommission', 'Commission plateforme', seed."PlatformCommissionAmount", 'XOF', 30, seed."CreatedAt", seed."CreatedAt"
FROM "WeleDemoMissionSeed" seed
UNION ALL
SELECT gen_random_uuid(), seed."MissionId", 'CompanyPayout', 'Reversement entreprise', seed."CompanyPayoutAmount", 'XOF', 40, seed."CreatedAt", seed."CreatedAt"
FROM "WeleDemoMissionSeed" seed;

INSERT INTO "MissionPaymentMilestones" (
    "Id",
    "MissionId",
    "Trigger",
    "Status",
    "Amount",
    "Currency",
    "Label",
    "SortOrder",
    "DueAt",
    "PaidAt",
    "ExternalPaymentReference",
    "CreatedAt",
    "UpdatedAt"
)
SELECT
    gen_random_uuid(),
    seed."MissionId",
    milestone."Trigger",
    CASE
        WHEN seed."Status" = 'Completed' THEN 'Paid'
        ELSE 'Pending'
    END,
    milestone."Amount",
    'XOF',
    milestone."Label",
    milestone."SortOrder",
    seed."CreatedAt" + (milestone."SortOrder" || ' hours')::interval,
    CASE
        WHEN seed."Status" = 'Completed' THEN seed."CreatedAt" + (milestone."SortOrder" || ' hours')::interval
        ELSE seed."CreatedAt" + (milestone."SortOrder" || ' hours')::interval
    END,
    CASE
        WHEN seed."Status" = 'Completed'
            THEN 'DEMO-' || seed."PaymentMethod" || '-' || seed."SlotNumber" || '-' || milestone."SortOrder"
        ELSE 'DEMO-' || seed."PaymentMethod" || '-' || seed."SlotNumber" || '-' || milestone."SortOrder"
    END,
    seed."CreatedAt",
    seed."CreatedAt"
FROM "WeleDemoMissionSeed" seed
CROSS JOIN LATERAL (
    VALUES
        ('QuoteAccepted', ROUND(seed."CompanyQuotedAmount" * 0.15)::integer, 'Commission a l''acceptation', 1),
        ('MissionStarted', ROUND(seed."CompanyQuotedAmount" * 0.38)::integer, 'Acompte demarrage', 2),
        ('MissionCompleted', seed."CompanyQuotedAmount" - ROUND(seed."CompanyQuotedAmount" * 0.15)::integer - ROUND(seed."CompanyQuotedAmount" * 0.38)::integer, 'Solde fin de mission', 3)
) AS milestone("Trigger", "Amount", "Label", "SortOrder");

DROP TABLE IF EXISTS "WeleDemoMissionSeed";
