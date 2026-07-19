-- Repair legacy ProviderServices schema so new employee service assignments can be inserted.
-- Safe to run multiple times.

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
