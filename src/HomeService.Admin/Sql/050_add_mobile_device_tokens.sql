CREATE TABLE IF NOT EXISTS "MobileDeviceTokens" (
    "Id" uuid NOT NULL,
    "OwnerType" character varying(32) NOT NULL,
    "OwnerId" uuid NOT NULL,
    "Platform" character varying(32) NOT NULL,
    "Token" character varying(4096) NOT NULL,
    "DeviceLabel" character varying(120),
    "IsActive" boolean NOT NULL,
    "LastSeenAt" timestamp with time zone NOT NULL,
    "DisabledAt" timestamp with time zone,
    "FailureReason" character varying(500),
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_MobileDeviceTokens" PRIMARY KEY ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_MobileDeviceTokens_OwnerType_OwnerId_IsActive"
    ON "MobileDeviceTokens" ("OwnerType", "OwnerId", "IsActive");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_MobileDeviceTokens_Token"
    ON "MobileDeviceTokens" ("Token");
