-- Repair legacy ProviderServices schema so new employee service assignments can be inserted.
-- Safe to run multiple times.

ALTER TABLE "ProviderServices"
    ADD COLUMN IF NOT EXISTS "CompanyId" uuid,
    ADD COLUMN IF NOT EXISTS "PriceTier" character varying(32) NOT NULL DEFAULT 'Normal',
    ADD COLUMN IF NOT EXISTS "PricingUnit" character varying(32) NOT NULL DEFAULT 'Hourly';

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
