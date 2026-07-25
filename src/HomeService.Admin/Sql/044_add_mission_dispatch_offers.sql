ALTER TABLE "Companies"
ADD COLUMN IF NOT EXISTS "AcceptsUrgentMissions" boolean NOT NULL DEFAULT false;

ALTER TABLE "Companies"
ADD COLUMN IF NOT EXISTS "MissionDispatchPriority" integer NOT NULL DEFAULT 100;

CREATE TABLE IF NOT EXISTS "MissionDispatchOffers" (
    "Id" uuid NOT NULL,
    "MissionId" uuid NOT NULL,
    "CompanyId" uuid NOT NULL,
    "Rank" integer NOT NULL,
    "Score" integer NOT NULL,
    "ScoreDetails" character varying(1200) NOT NULL,
    "Status" character varying(32) NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "RespondedAt" timestamp with time zone NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NULL,
    CONSTRAINT "PK_MissionDispatchOffers" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_MissionDispatchOffers_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_MissionDispatchOffers_Missions_MissionId" FOREIGN KEY ("MissionId") REFERENCES "Missions" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_Companies_Status_MissionDispatchPriority"
ON "Companies" ("Status", "MissionDispatchPriority");

CREATE INDEX IF NOT EXISTS "IX_MissionDispatchOffers_CompanyId_Status_ExpiresAt"
ON "MissionDispatchOffers" ("CompanyId", "Status", "ExpiresAt");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_MissionDispatchOffers_MissionId_CompanyId"
ON "MissionDispatchOffers" ("MissionId", "CompanyId");

CREATE INDEX IF NOT EXISTS "IX_MissionDispatchOffers_MissionId_Status_Rank"
ON "MissionDispatchOffers" ("MissionId", "Status", "Rank");
