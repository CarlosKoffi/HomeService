ALTER TABLE "ServicePrestations"
    ADD COLUMN IF NOT EXISTS "IllustrationUrl" character varying(1000);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260730233701_AddServicePrestationIllustration', '9.0.0'
WHERE NOT EXISTS (
    SELECT 1
    FROM "__EFMigrationsHistory"
    WHERE "MigrationId" = '20260730233701_AddServicePrestationIllustration'
);
