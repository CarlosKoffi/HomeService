ALTER TABLE "Missions"
    ADD COLUMN IF NOT EXISTS "CancellationComment" character varying(1200),
    ADD COLUMN IF NOT EXISTS "CancellationReason" character varying(64),
    ADD COLUMN IF NOT EXISTS "CancelledAt" timestamp with time zone,
    ADD COLUMN IF NOT EXISTS "CancelledBy" character varying(32),
    ADD COLUMN IF NOT EXISTS "RefundAmount" integer NOT NULL DEFAULT 0;

CREATE INDEX IF NOT EXISTS "IX_Missions_CancelledAt_CancelledBy"
    ON "Missions" ("CancelledAt", "CancelledBy");
