ALTER TABLE "Missions"
    ADD COLUMN IF NOT EXISTS "MissionNumber" character varying(32);

UPDATE "Missions"
SET "MissionNumber" = upper(concat(
    'MIS-',
    to_char(coalesce("CreatedAt", now()), 'YYMMDD'),
    '-',
    substr(replace("Id"::text, '-', ''), 1, 8)
))
WHERE "MissionNumber" IS NULL
   OR trim("MissionNumber") = '';

ALTER TABLE "Missions"
    ALTER COLUMN "MissionNumber" SET NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_Missions_MissionNumber"
    ON "Missions" ("MissionNumber");
