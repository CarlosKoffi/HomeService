ALTER TABLE "ServicePrestations"
ADD COLUMN IF NOT EXISTS "NormalPriceAmount" integer NOT NULL DEFAULT 0;

ALTER TABLE "ServicePrestations"
ADD COLUMN IF NOT EXISTS "PremiumPriceAmount" integer NOT NULL DEFAULT 0;

ALTER TABLE "ServicePrestations"
ADD COLUMN IF NOT EXISTS "Currency" character varying(8) NOT NULL DEFAULT 'XOF';

UPDATE "ServicePrestations" AS prestation
SET
    "NormalPriceAmount" = pricing."NormalPriceAmount",
    "PremiumPriceAmount" = pricing."PremiumPriceAmount",
    "Currency" = pricing."Currency"
FROM (
    VALUES
        ('jardinage', 'tondre le gazon', 4500, 6500, 'XOF'),
        ('jardinage', 'tailler une haie', 5500, 7500, 'XOF'),
        ('jardinage', 'desherbage', 3500, 5000, 'XOF'),
        ('menage a domicile', 'menage regulier', 3500, 5000, 'XOF'),
        ('menage a domicile', 'nettoyage apres travaux', 5000, 7000, 'XOF'),
        ('menage a domicile', 'nettoyage vitres', 3000, 4500, 'XOF'),
        ('nounou', 'garde ponctuelle', 4000, 6500, 'XOF'),
        ('nounou', 'garde apres ecole', 4500, 7000, 'XOF')
) AS pricing("ServiceNormalizedName", "PrestationNormalizedName", "NormalPriceAmount", "PremiumPriceAmount", "Currency"),
"Services" AS service
WHERE service."Id" = prestation."ServiceId"
  AND service."NormalizedName" = pricing."ServiceNormalizedName"
  AND prestation."NormalizedName" = pricing."PrestationNormalizedName";

CREATE TABLE IF NOT EXISTS "ProviderServicePrestations" (
    "Id" uuid NOT NULL,
    "ProviderServiceId" uuid NOT NULL,
    "ServicePrestationId" uuid NOT NULL,
    "IsActive" boolean NOT NULL DEFAULT true,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NULL,
    CONSTRAINT "PK_ProviderServicePrestations" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ProviderServicePrestations_ProviderServices_ProviderServiceId"
        FOREIGN KEY ("ProviderServiceId") REFERENCES "ProviderServices" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_ProviderServicePrestations_ServicePrestations_ServicePrestationId"
        FOREIGN KEY ("ServicePrestationId") REFERENCES "ServicePrestations" ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_ProviderServicePrestations_ProviderServiceId_ServicePrestationId"
ON "ProviderServicePrestations" ("ProviderServiceId", "ServicePrestationId");

CREATE INDEX IF NOT EXISTS "IX_ProviderServicePrestations_ServicePrestationId_IsActive"
ON "ProviderServicePrestations" ("ServicePrestationId", "IsActive");
