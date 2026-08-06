-- Test/staging helper requested for Wélé Entreprise validation.
--
-- Every company portal user receives the temporary password: testeur12345
-- Existing active/inactive flags are preserved. All company sessions are revoked
-- so the temporary password is required on the next login.
-- The stored value uses the current PBKDF2-SHA256 format; the clear-text password
-- is never written to the database.
--
-- Review the target database and take a backup before applying this script.

BEGIN;

DELETE FROM "CompanyPortalSessions";

UPDATE "CompanyPortalUsers"
SET "PasswordHash" = 'pbkdf2-sha256:210000:ujkrPXile6wimyqDsvluxw==:2YEeBWl5HwAdNmKrdqzbwiytYQeQJROIWqIgHBCJb34=',
    "UpdatedAt" = now();

COMMIT;
