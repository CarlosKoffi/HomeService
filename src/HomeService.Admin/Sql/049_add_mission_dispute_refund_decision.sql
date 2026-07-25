ALTER TABLE "MissionDisputes"
    ADD COLUMN IF NOT EXISTS "Currency" character varying(3) NOT NULL DEFAULT 'XOF',
    ADD COLUMN IF NOT EXISTS "RefundAmount" integer,
    ADD COLUMN IF NOT EXISTS "RefundPercentBasisPoints" integer;

UPDATE "MissionDisputes"
SET "Currency" = 'XOF'
WHERE "Currency" IS NULL OR "Currency" = '';
