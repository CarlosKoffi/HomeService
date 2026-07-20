-- Normalize existing catalog names with the same accent/separator-tolerant intent as the domain model.
-- Safe to run multiple times. Rows that would collide with an existing normalized key are left unchanged
-- so an admin can merge them manually from the service catalogue screen.

CREATE OR REPLACE FUNCTION wele_normalize_catalog_name(value text)
RETURNS text
LANGUAGE sql
IMMUTABLE
AS $$
    SELECT trim(regexp_replace(
        translate(
            lower(coalesce(value, '')),
            'àáâäãåçèéêëìíîïñòóôöõùúûüýÿ',
            'aaaaaaceeeeiiiinooooouuuuyy'
        ),
        '[^a-z0-9]+',
        ' ',
        'g'
    ));
$$;

WITH normalized_services AS (
    SELECT
        "Id",
        wele_normalize_catalog_name("Name") AS "NextNormalizedName"
    FROM "Services"
),
safe_services AS (
    SELECT item."Id", item."NextNormalizedName"
    FROM normalized_services item
    WHERE item."NextNormalizedName" <> ''
      AND NOT EXISTS (
          SELECT 1
          FROM normalized_services duplicate
          WHERE duplicate."Id" <> item."Id"
            AND duplicate."NextNormalizedName" = item."NextNormalizedName"
      )
)
UPDATE "Services" service
SET "NormalizedName" = safe."NextNormalizedName"
FROM safe_services safe
WHERE service."Id" = safe."Id"
  AND service."NormalizedName" <> safe."NextNormalizedName";

WITH normalized_prestations AS (
    SELECT
        "Id",
        "ServiceId",
        wele_normalize_catalog_name("Name") AS "NextNormalizedName"
    FROM "ServicePrestations"
),
safe_prestations AS (
    SELECT item."Id", item."NextNormalizedName"
    FROM normalized_prestations item
    WHERE item."NextNormalizedName" <> ''
      AND NOT EXISTS (
          SELECT 1
          FROM normalized_prestations duplicate
          WHERE duplicate."Id" <> item."Id"
            AND duplicate."ServiceId" = item."ServiceId"
            AND duplicate."NextNormalizedName" = item."NextNormalizedName"
      )
)
UPDATE "ServicePrestations" prestation
SET "NormalizedName" = safe."NextNormalizedName"
FROM safe_prestations safe
WHERE prestation."Id" = safe."Id"
  AND prestation."NormalizedName" <> safe."NextNormalizedName";

UPDATE "CompanyApplicationServices"
SET "NormalizedName" = wele_normalize_catalog_name("RawName")
WHERE wele_normalize_catalog_name("RawName") <> ''
  AND "NormalizedName" <> wele_normalize_catalog_name("RawName");

DROP FUNCTION wele_normalize_catalog_name(text);
