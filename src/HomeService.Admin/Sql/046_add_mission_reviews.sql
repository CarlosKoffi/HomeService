ALTER TABLE "Missions"
    ADD COLUMN IF NOT EXISTS "CustomerCompletionValidatedAt" timestamp with time zone NULL;

ALTER TABLE "Missions"
    ADD COLUMN IF NOT EXISTS "CompanyPayoutReleasedAt" timestamp with time zone NULL;

CREATE TABLE IF NOT EXISTS "MissionReviews" (
    "Id" uuid NOT NULL,
    "MissionId" uuid NOT NULL,
    "CustomerId" uuid NOT NULL,
    "CompanyId" uuid NOT NULL,
    "ProviderId" uuid NOT NULL,
    "QualityRating" integer NOT NULL,
    "PunctualityRating" integer NOT NULL,
    "PolitenessRating" integer NOT NULL,
    "CleanlinessRating" integer NOT NULL,
    "OverallRating" integer NOT NULL,
    "Comment" character varying(1200) NULL,
    "SubmittedAt" timestamp with time zone NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NULL,
    CONSTRAINT "PK_MissionReviews" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_MissionReviews_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MissionReviews_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MissionReviews_Missions_MissionId" FOREIGN KEY ("MissionId") REFERENCES "Missions" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_MissionReviews_Providers_ProviderId" FOREIGN KEY ("ProviderId") REFERENCES "Providers" ("Id") ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS "IX_Missions_CustomerCompletionValidatedAt_CompanyPayoutReleasedAt"
    ON "Missions" ("CustomerCompletionValidatedAt", "CompanyPayoutReleasedAt");

CREATE INDEX IF NOT EXISTS "IX_MissionReviews_CompanyId_SubmittedAt"
    ON "MissionReviews" ("CompanyId", "SubmittedAt");

CREATE INDEX IF NOT EXISTS "IX_MissionReviews_CustomerId"
    ON "MissionReviews" ("CustomerId");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_MissionReviews_MissionId"
    ON "MissionReviews" ("MissionId");

CREATE INDEX IF NOT EXISTS "IX_MissionReviews_ProviderId_SubmittedAt"
    ON "MissionReviews" ("ProviderId", "SubmittedAt");
