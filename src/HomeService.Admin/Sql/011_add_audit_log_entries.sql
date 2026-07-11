CREATE TABLE IF NOT EXISTS "AuditLogEntries" (
    "Id" uuid NOT NULL,
    "ActorType" character varying(32) NOT NULL,
    "ActorId" uuid NULL,
    "ActorDisplayName" character varying(180) NULL,
    "Action" character varying(120) NOT NULL,
    "EntityType" character varying(160) NOT NULL,
    "EntityId" uuid NULL,
    "Summary" character varying(1000) NULL,
    "BeforeJson" jsonb NULL,
    "AfterJson" jsonb NULL,
    "IpAddress" character varying(80) NULL,
    "UserAgent" character varying(500) NULL,
    "CorrelationId" character varying(120) NULL,
    "OccurredAt" timestamp with time zone NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NULL,
    CONSTRAINT "PK_AuditLogEntries" PRIMARY KEY ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_AuditLogEntries_OccurredAt" ON "AuditLogEntries" ("OccurredAt");
CREATE INDEX IF NOT EXISTS "IX_AuditLogEntries_ActorType_ActorId_OccurredAt" ON "AuditLogEntries" ("ActorType", "ActorId", "OccurredAt");
CREATE INDEX IF NOT EXISTS "IX_AuditLogEntries_EntityType_EntityId_OccurredAt" ON "AuditLogEntries" ("EntityType", "EntityId", "OccurredAt");
CREATE INDEX IF NOT EXISTS "IX_AuditLogEntries_CorrelationId" ON "AuditLogEntries" ("CorrelationId");
