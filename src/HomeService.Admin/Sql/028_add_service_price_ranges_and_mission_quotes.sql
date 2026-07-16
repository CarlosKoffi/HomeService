ALTER TABLE "Services"
ADD COLUMN IF NOT EXISTS "PriceMinAmount" integer NOT NULL DEFAULT 1500;

ALTER TABLE "Services"
ADD COLUMN IF NOT EXISTS "PriceMaxAmount" integer NOT NULL DEFAULT 2500;

UPDATE "Services"
SET "PriceMinAmount" = GREATEST(0, "NormalPriceAmount"),
    "PriceMaxAmount" = GREATEST(GREATEST(0, "NormalPriceAmount"), "PremiumPriceAmount");

ALTER TABLE "ServicePrestations"
ADD COLUMN IF NOT EXISTS "PriceMinAmount" integer NOT NULL DEFAULT 0;

ALTER TABLE "ServicePrestations"
ADD COLUMN IF NOT EXISTS "PriceMaxAmount" integer NOT NULL DEFAULT 0;

UPDATE "ServicePrestations"
SET "PriceMinAmount" = GREATEST(0, "NormalPriceAmount"),
    "PriceMaxAmount" = GREATEST(GREATEST(0, "NormalPriceAmount"), "PremiumPriceAmount");

ALTER TABLE "Missions"
ADD COLUMN IF NOT EXISTS "CompanyQuotedAmount" integer NULL;

ALTER TABLE "Missions"
ADD COLUMN IF NOT EXISTS "CompanyQuoteJustification" text NULL;

ALTER TABLE "Missions"
ADD COLUMN IF NOT EXISTS "CompanyQuotedAt" timestamp with time zone NULL;

ALTER TABLE "Missions"
ADD COLUMN IF NOT EXISTS "CustomerQuoteAcceptedAt" timestamp with time zone NULL;
