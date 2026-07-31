ALTER TABLE "Services"
    ADD COLUMN IF NOT EXISTS "DisplayCategory" character varying(24) NOT NULL DEFAULT 'Home';

UPDATE "Services"
SET "DisplayCategory" = 'Wellbeing'
WHERE lower("Name") SIMILAR TO '%(coiff|massage|ongler|barbier|maquillage|estheti|beaute|bien-etre)%';

UPDATE "Services"
SET "DisplayCategory" = 'Home'
WHERE lower("Name") SIMILAR TO '%(nounou|garde|menage|jardin|plomb|electr|clim|serrur|blanch|repass|auto|peinture|demenag)%';

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260731090118_AddServiceDisplayCategory', '9.0.0'
WHERE NOT EXISTS (
    SELECT 1
    FROM "__EFMigrationsHistory"
    WHERE "MigrationId" = '20260731090118_AddServiceDisplayCategory'
);
