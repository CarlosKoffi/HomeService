ALTER TABLE "Companies"
    ADD COLUMN IF NOT EXISTS "AcceptsInterimApplications" boolean NOT NULL DEFAULT false;

CREATE INDEX IF NOT EXISTS "IX_Companies_AcceptsInterimApplications_Status"
    ON "Companies" ("AcceptsInterimApplications", "Status");
