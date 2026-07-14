ALTER TABLE "Providers"
    DROP CONSTRAINT IF EXISTS "FK_Providers_Companies_CompanyId";

ALTER TABLE "Providers"
    ALTER COLUMN "CompanyId" DROP NOT NULL;

ALTER TABLE "Providers"
    ADD COLUMN IF NOT EXISTS "RegistrationSource" character varying(32) NOT NULL DEFAULT 'CompanyInvitation';

ALTER TABLE "Providers"
    ADD CONSTRAINT "FK_Providers_Companies_CompanyId"
    FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id");

CREATE INDEX IF NOT EXISTS "IX_Providers_Status"
    ON "Providers" ("Status");

CREATE TABLE IF NOT EXISTS "ProviderAffiliationRequests" (
    "Id" uuid NOT NULL,
    "ProviderId" uuid NOT NULL,
    "CompanyId" uuid NOT NULL,
    "Status" character varying(32) NOT NULL,
    "Message" character varying(800) NULL,
    "ReviewNote" character varying(800) NULL,
    "RequestedAt" timestamp with time zone NOT NULL,
    "ReviewedAt" timestamp with time zone NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NULL,
    CONSTRAINT "PK_ProviderAffiliationRequests" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ProviderAffiliationRequests_Providers_ProviderId" FOREIGN KEY ("ProviderId") REFERENCES "Providers" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_ProviderAffiliationRequests_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS "ProviderCandidateServices" (
    "Id" uuid NOT NULL,
    "ProviderId" uuid NOT NULL,
    "ServiceId" uuid NOT NULL,
    "ExperienceLevel" character varying(32) NOT NULL,
    "YearsOfExperience" integer NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NULL,
    CONSTRAINT "PK_ProviderCandidateServices" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ProviderCandidateServices_Providers_ProviderId" FOREIGN KEY ("ProviderId") REFERENCES "Providers" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_ProviderCandidateServices_Services_ServiceId" FOREIGN KEY ("ServiceId") REFERENCES "Services" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_ProviderAffiliationRequests_CompanyId_Status_RequestedAt"
    ON "ProviderAffiliationRequests" ("CompanyId", "Status", "RequestedAt");

CREATE INDEX IF NOT EXISTS "IX_ProviderAffiliationRequests_ProviderId_CompanyId_Status"
    ON "ProviderAffiliationRequests" ("ProviderId", "CompanyId", "Status");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_ProviderCandidateServices_ProviderId_ServiceId"
    ON "ProviderCandidateServices" ("ProviderId", "ServiceId");

CREATE INDEX IF NOT EXISTS "IX_ProviderCandidateServices_ServiceId_IsActive"
    ON "ProviderCandidateServices" ("ServiceId", "IsActive");
