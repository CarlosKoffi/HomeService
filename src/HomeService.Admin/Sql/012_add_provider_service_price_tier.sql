ALTER TABLE "ProviderServices"
ADD COLUMN IF NOT EXISTS "PriceTier" character varying(32) NOT NULL DEFAULT 'Normal';

UPDATE "ProviderServices"
SET "PriceTier" = 'Normal'
WHERE "PriceTier" IS NULL OR "PriceTier" = '';
