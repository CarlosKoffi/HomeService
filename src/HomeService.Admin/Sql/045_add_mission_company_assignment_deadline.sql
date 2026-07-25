ALTER TABLE "Missions"
ADD COLUMN IF NOT EXISTS "CompanyAssignmentExpiresAt" timestamp with time zone NULL;

CREATE INDEX IF NOT EXISTS "IX_Missions_CompanyAssignmentExpiresAt_Status"
ON "Missions" ("CompanyAssignmentExpiresAt", "Status");
