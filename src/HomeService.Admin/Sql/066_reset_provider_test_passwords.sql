-- Test/staging helper requested for provider application validation.
--
-- Every provider account receives the temporary password: testeur12345
-- Active provider sessions are revoked so the new password is required immediately.
-- The legacy SHA-256 format is intentionally used only for this operational reset:
-- ProviderPortalAuthService replaces it with a uniquely salted PBKDF2 hash after the
-- provider's first successful login.
--
-- Review the target database and take a backup before applying this script.

BEGIN;

DELETE FROM "ProviderPortalSessions";

UPDATE "Providers"
SET "PasswordHash" = 'sha256:wele-test-providers-20260805:6dd2805cf29e21d80f89d69d790d44d1a2e74d9b66d6e6f5fe18eb52caca5b26',
    "UpdatedAt" = now();

COMMIT;
