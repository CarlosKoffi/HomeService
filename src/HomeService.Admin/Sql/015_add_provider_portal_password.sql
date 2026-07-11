ALTER TABLE "Providers"
ADD COLUMN IF NOT EXISTS "PasswordHash" character varying(256);
