ALTER TABLE "Services"
ADD COLUMN IF NOT EXISTS "Currency" character varying(3) NOT NULL DEFAULT 'XOF';

ALTER TABLE "Services"
ADD COLUMN IF NOT EXISTS "NormalPriceAmount" integer NOT NULL DEFAULT 1500;

ALTER TABLE "Services"
ADD COLUMN IF NOT EXISTS "PremiumPriceAmount" integer NOT NULL DEFAULT 2500;

UPDATE "Services"
SET "Currency" = 'XOF',
    "NormalPriceAmount" = CASE
        WHEN "NormalizedName" = 'menage a domicile' THEN 3500
        WHEN "NormalizedName" = 'nounou' THEN 4000
        WHEN "NormalizedName" = 'jardinage' THEN 4500
        ELSE "NormalPriceAmount"
    END,
    "PremiumPriceAmount" = CASE
        WHEN "NormalizedName" = 'menage a domicile' THEN 5000
        WHEN "NormalizedName" = 'nounou' THEN 6500
        WHEN "NormalizedName" = 'jardinage' THEN 6500
        ELSE "PremiumPriceAmount"
    END;

ALTER TABLE "ProviderServices"
DROP COLUMN IF EXISTS "HourlyRateAmount";

ALTER TABLE "ProviderServices"
DROP COLUMN IF EXISTS "Currency";
