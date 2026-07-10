-- Company portal employee workspace.
-- Keep this script aligned with EF migration AddCompanyPortalEmployeeWorkspace.

ALTER TABLE "Providers"
    ADD COLUMN IF NOT EXISTS "Address" character varying(300) NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS "DateOfBirth" date NULL,
    ADD COLUMN IF NOT EXISTS "EmploymentType" character varying(40) NOT NULL DEFAULT 'CompanyEmployee',
    ADD COLUMN IF NOT EXISTS "MissionLatitude" numeric(10,7) NULL,
    ADD COLUMN IF NOT EXISTS "MissionLongitude" numeric(10,7) NULL,
    ADD COLUMN IF NOT EXISTS "MissionRadiusKm" integer NOT NULL DEFAULT 5,
    ADD COLUMN IF NOT EXISTS "YearsOfExperience" integer NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS "ProviderDocuments" (
    "Id" uuid NOT NULL,
    "ProviderId" uuid NOT NULL,
    "DocumentType" character varying(40) NOT NULL,
    "OriginalFileName" character varying(260) NOT NULL,
    "StoragePath" character varying(700) NOT NULL,
    "ContentType" character varying(120) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NULL,
    CONSTRAINT "PK_ProviderDocuments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ProviderDocuments_Providers_ProviderId" FOREIGN KEY ("ProviderId") REFERENCES "Providers" ("Id") ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS "CompanyPortalSessions" (
    "Id" uuid NOT NULL,
    "CompanyPortalUserId" uuid NOT NULL,
    "TokenHash" character varying(128) NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "RevokedAt" timestamp with time zone NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NULL,
    CONSTRAINT "PK_CompanyPortalSessions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_CompanyPortalSessions_CompanyPortalUsers_CompanyPortalUserId" FOREIGN KEY ("CompanyPortalUserId") REFERENCES "CompanyPortalUsers" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_Providers_CompanyId_Status" ON "Providers" ("CompanyId", "Status");
CREATE INDEX IF NOT EXISTS "IX_ProviderDocuments_ProviderId_DocumentType" ON "ProviderDocuments" ("ProviderId", "DocumentType");
CREATE INDEX IF NOT EXISTS "IX_ProviderServices_ServiceId" ON "ProviderServices" ("ServiceId");
CREATE INDEX IF NOT EXISTS "IX_CompanyPortalSessions_CompanyPortalUserId_ExpiresAt" ON "CompanyPortalSessions" ("CompanyPortalUserId", "ExpiresAt");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_CompanyPortalSessions_TokenHash" ON "CompanyPortalSessions" ("TokenHash");
