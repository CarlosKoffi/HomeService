-- Temporary operational helper while the provider mobile application is unavailable.
-- Availability is enabled only for providers already approved through the normal
-- validation workflow. No pending, incomplete, suspended or inactive profile is approved.
UPDATE "Providers"
SET "IsAvailable" = TRUE,
    "UpdatedAt" = now()
WHERE "CompanyId" IS NOT NULL
  AND "Status" = 'Approved'
  AND "IsAvailable" = FALSE;
