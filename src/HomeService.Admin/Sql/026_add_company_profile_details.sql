ALTER TABLE "Companies"
    ADD COLUMN IF NOT EXISTS "LegalForm" character varying(80),
    ADD COLUMN IF NOT EXISTS "RegistrationNumber" character varying(80),
    ADD COLUMN IF NOT EXISTS "TaxIdentificationNumber" character varying(80),
    ADD COLUMN IF NOT EXISTS "City" character varying(120),
    ADD COLUMN IF NOT EXISTS "Address" character varying(240),
    ADD COLUMN IF NOT EXISTS "InterventionZones" character varying(1000),
    ADD COLUMN IF NOT EXISTS "PlannedServices" character varying(1000),
    ADD COLUMN IF NOT EXISTS "WavePaymentNumber" character varying(32),
    ADD COLUMN IF NOT EXISTS "OrangeMoneyPaymentNumber" character varying(32);

ALTER TABLE "CompanyApplications"
    ADD COLUMN IF NOT EXISTS "LegalForm" character varying(80),
    ADD COLUMN IF NOT EXISTS "TaxIdentificationNumber" character varying(80),
    ADD COLUMN IF NOT EXISTS "InterventionZones" character varying(1000),
    ADD COLUMN IF NOT EXISTS "WavePaymentNumber" character varying(32),
    ADD COLUMN IF NOT EXISTS "OrangeMoneyPaymentNumber" character varying(32);

WITH latest_application AS (
    SELECT DISTINCT ON ("CompanyId")
        "CompanyId",
        "CompanyName",
        "RegistrationNumber",
        "City",
        "Address",
        "PlannedServices",
        "PhoneNumber",
        "Email"
    FROM "CompanyApplications"
    WHERE "CompanyId" IS NOT NULL
    ORDER BY "CompanyId", "CreatedAt" DESC
)
UPDATE "Companies" company
SET
    "Name" = COALESCE(NULLIF(company."Name", ''), latest_application."CompanyName"),
    "RegistrationNumber" = COALESCE(company."RegistrationNumber", latest_application."RegistrationNumber"),
    "City" = COALESCE(company."City", latest_application."City"),
    "Address" = COALESCE(company."Address", latest_application."Address"),
    "PlannedServices" = COALESCE(company."PlannedServices", latest_application."PlannedServices"),
    "PhoneNumber" = COALESCE(NULLIF(company."PhoneNumber", ''), latest_application."PhoneNumber"),
    "Email" = COALESCE(company."Email", latest_application."Email")
FROM latest_application
WHERE company."Id" = latest_application."CompanyId";
