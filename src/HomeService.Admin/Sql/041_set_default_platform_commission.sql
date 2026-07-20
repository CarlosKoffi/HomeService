-- Ensures the default wélé connection commission is 15%.
-- Idempotent: safe to run several times during deployments.

UPDATE "CommissionRules"
SET "Name" = 'Commission mise en relation wélé',
    "RateBasisPoints" = 1500,
    "FixedAmount" = 0,
    "Currency" = 'XOF',
    "IsActive" = true,
    "EffectiveUntil" = NULL,
    "UpdatedAt" = now()
WHERE "Target" = 'PlatformConnection'
  AND "ServiceId" IS NULL
  AND "ServicePrestationId" IS NULL
  AND "CompanyId" IS NULL
  AND "AssignmentSource" IS NULL;

INSERT INTO "CommissionRules"
    ("Id", "Name", "Target", "ServiceId", "ServicePrestationId", "CompanyId", "AssignmentSource",
     "RateBasisPoints", "FixedAmount", "Currency", "EffectiveFrom", "EffectiveUntil", "IsActive",
     "CreatedAt", "UpdatedAt")
SELECT gen_random_uuid(), 'Commission mise en relation wélé', 'PlatformConnection',
       NULL, NULL, NULL, NULL, 1500, 0, 'XOF', now(), NULL, true, now(), now()
WHERE NOT EXISTS (
    SELECT 1
    FROM "CommissionRules"
    WHERE "Target" = 'PlatformConnection'
      AND "ServiceId" IS NULL
      AND "ServicePrestationId" IS NULL
      AND "CompanyId" IS NULL
      AND "AssignmentSource" IS NULL
);
