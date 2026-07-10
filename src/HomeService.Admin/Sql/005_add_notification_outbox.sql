CREATE TABLE IF NOT EXISTS "NotificationOutboxMessages" (
    "Id" uuid NOT NULL,
    "Channel" character varying(32) NOT NULL,
    "Status" character varying(32) NOT NULL,
    "Recipient" character varying(256) NOT NULL,
    "Subject" character varying(180) NOT NULL,
    "Body" character varying(2000) NOT NULL,
    "RelatedEntityType" character varying(80),
    "RelatedEntityId" uuid,
    "MetadataJson" jsonb,
    "ScheduledAt" timestamp with time zone NOT NULL,
    "SentAt" timestamp with time zone,
    "FailureReason" character varying(1000),
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_NotificationOutboxMessages" PRIMARY KEY ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_NotificationOutboxMessages_RelatedEntityType_RelatedEntityId"
    ON "NotificationOutboxMessages" ("RelatedEntityType", "RelatedEntityId");

CREATE INDEX IF NOT EXISTS "IX_NotificationOutboxMessages_Status_Channel_ScheduledAt"
    ON "NotificationOutboxMessages" ("Status", "Channel", "ScheduledAt");
