CREATE TABLE IF NOT EXISTS "CompanyPortalActivities" (
    "Id" uuid NOT NULL,
    "CompanyId" uuid NOT NULL,
    "Type" character varying(64) NOT NULL,
    "Title" character varying(160) NOT NULL,
    "Description" character varying(320) NOT NULL,
    "Tone" character varying(32) NOT NULL,
    "EntityType" character varying(96),
    "EntityId" uuid,
    "OccurredAt" timestamp with time zone NOT NULL,
    "IsRead" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_CompanyPortalActivities" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_CompanyPortalActivities_Companies_CompanyId"
        FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_CompanyPortalActivities_CompanyId_IsRead_OccurredAt"
    ON "CompanyPortalActivities" ("CompanyId", "IsRead", "OccurredAt");

CREATE INDEX IF NOT EXISTS "IX_CompanyPortalActivities_CompanyId_OccurredAt"
    ON "CompanyPortalActivities" ("CompanyId", "OccurredAt");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260715182725_AddCompanyPortalActivities', '9.0.0'
WHERE EXISTS (
    SELECT 1
    FROM information_schema.tables
    WHERE table_name = '__EFMigrationsHistory'
)
AND NOT EXISTS (
    SELECT 1
    FROM "__EFMigrationsHistory"
    WHERE "MigrationId" = '20260715182725_AddCompanyPortalActivities'
);
