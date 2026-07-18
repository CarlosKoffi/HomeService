CREATE TABLE IF NOT EXISTS "CompanyPortalNotifications" (
    "Id" uuid NOT NULL,
    "CompanyId" uuid NOT NULL,
    "CompanyApplicationId" uuid,
    "CompanyApplicationDocumentId" uuid,
    "Type" character varying(64) NOT NULL,
    "Title" character varying(160) NOT NULL,
    "Message" character varying(700) NOT NULL,
    "Tone" character varying(32) NOT NULL,
    "ActionUrl" character varying(240),
    "IsRead" boolean NOT NULL,
    "OccurredAt" timestamp with time zone NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_CompanyPortalNotifications" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_CompanyPortalNotifications_Companies"
        FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_CompanyPortalNotifications_CompanyApplications"
        FOREIGN KEY ("CompanyApplicationId") REFERENCES "CompanyApplications" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_CompanyPortalNotifications_CompanyApplicationDocuments"
        FOREIGN KEY ("CompanyApplicationDocumentId") REFERENCES "CompanyApplicationDocuments" ("Id") ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS "IX_CompanyPortalNotifications_CompanyId_IsRead_OccurredAt"
    ON "CompanyPortalNotifications" ("CompanyId", "IsRead", "OccurredAt");

CREATE INDEX IF NOT EXISTS "IX_CompanyPortalNotifications_CompanyApplicationId_OccurredAt"
    ON "CompanyPortalNotifications" ("CompanyApplicationId", "OccurredAt");

CREATE INDEX IF NOT EXISTS "IX_CompanyPortalNotifications_CompanyApplicationDocumentId_OccurredAt"
    ON "CompanyPortalNotifications" ("CompanyApplicationDocumentId", "OccurredAt");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260718132905_AddCompanyPortalNotifications', '9.0.0'
WHERE EXISTS (
    SELECT 1
    FROM information_schema.tables
    WHERE table_name = '__EFMigrationsHistory'
)
AND NOT EXISTS (
    SELECT 1
    FROM "__EFMigrationsHistory"
    WHERE "MigrationId" = '20260718132905_AddCompanyPortalNotifications'
);
