-- Adds an optional email field to provider profiles so the company portal can
-- manage employee contact details without relying only on phone numbers.

ALTER TABLE "Providers"
    ADD COLUMN IF NOT EXISTS "Email" character varying(256);
