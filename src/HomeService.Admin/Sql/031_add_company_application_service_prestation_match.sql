ALTER TABLE "CompanyApplicationServices"
    ADD COLUMN IF NOT EXISTS "MatchedServicePrestationId" uuid;

CREATE INDEX IF NOT EXISTS "IX_CompanyApplicationServices_MatchedServicePrestationId"
    ON "CompanyApplicationServices" ("MatchedServicePrestationId");

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'FK_CompanyApplicationServices_ServicePrestations_MatchedServicePrestationId'
    ) THEN
        ALTER TABLE "CompanyApplicationServices"
            ADD CONSTRAINT "FK_CompanyApplicationServices_ServicePrestations_MatchedServicePrestationId"
            FOREIGN KEY ("MatchedServicePrestationId")
            REFERENCES "ServicePrestations" ("Id");
    END IF;
END $$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260718163904_AddCompanyApplicationServicePrestationMatch', '9.0.0'
WHERE NOT EXISTS (
    SELECT 1
    FROM "__EFMigrationsHistory"
    WHERE "MigrationId" = '20260718163904_AddCompanyApplicationServicePrestationMatch'
);
