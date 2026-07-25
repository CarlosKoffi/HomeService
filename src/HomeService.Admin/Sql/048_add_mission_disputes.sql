CREATE TABLE IF NOT EXISTS "MissionDisputes" (
    "Id" uuid NOT NULL,
    "MissionId" uuid NOT NULL,
    "Status" character varying(32) NOT NULL,
    "OpenedBy" character varying(32) NOT NULL,
    "Reason" character varying(64) NOT NULL,
    "Description" character varying(1200) NOT NULL,
    "Resolution" character varying(64),
    "ResolutionNote" character varying(1200),
    "OpenedAt" timestamp with time zone NOT NULL,
    "ResolvedAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_MissionDisputes" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_MissionDisputes_Missions_MissionId" FOREIGN KEY ("MissionId") REFERENCES "Missions" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_MissionDisputes_MissionId_Status"
    ON "MissionDisputes" ("MissionId", "Status");

CREATE INDEX IF NOT EXISTS "IX_MissionDisputes_Status_OpenedAt"
    ON "MissionDisputes" ("Status", "OpenedAt");
