ALTER TABLE "Missions"
    ADD COLUMN IF NOT EXISTS "PlatformCommissionAmount" integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "TransportFeeAmount" integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "CancellationFeeAmount" integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "ProviderAcceptedAt" timestamp with time zone NULL,
    ADD COLUMN IF NOT EXISTS "CustomerConfirmedAt" timestamp with time zone NULL,
    ADD COLUMN IF NOT EXISTS "ContactDetailsReleasedAt" timestamp with time zone NULL;
