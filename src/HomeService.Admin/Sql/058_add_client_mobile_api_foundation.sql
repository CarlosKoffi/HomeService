-- Client mobile API foundation: sessions, profile details, addresses and payment methods.
-- EF migration source: AddClientMobileApiFoundation.

ALTER TABLE "Customers"
    ADD COLUMN IF NOT EXISTS "Email" character varying(180);

ALTER TABLE "Customers"
    ADD COLUMN IF NOT EXISTS "PasswordHash" character varying(512);

CREATE INDEX IF NOT EXISTS "IX_Customers_Email"
    ON "Customers" ("Email");

CREATE INDEX IF NOT EXISTS "IX_Customers_PhoneNumber"
    ON "Customers" ("PhoneNumber");

CREATE TABLE IF NOT EXISTS "CustomerSessions" (
    "Id" uuid NOT NULL,
    "CustomerId" uuid NOT NULL,
    "TokenHash" character varying(128) NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "RevokedAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_CustomerSessions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_CustomerSessions_Customers_CustomerId" FOREIGN KEY ("CustomerId")
        REFERENCES "Customers" ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_CustomerSessions_TokenHash"
    ON "CustomerSessions" ("TokenHash");

CREATE INDEX IF NOT EXISTS "IX_CustomerSessions_CustomerId_ExpiresAt"
    ON "CustomerSessions" ("CustomerId", "ExpiresAt");

CREATE TABLE IF NOT EXISTS "CustomerAddresses" (
    "Id" uuid NOT NULL,
    "CustomerId" uuid NOT NULL,
    "Label" character varying(80) NOT NULL,
    "AddressLine" character varying(300) NOT NULL,
    "Latitude" numeric(10,7),
    "Longitude" numeric(10,7),
    "IsDefault" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_CustomerAddresses" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_CustomerAddresses_Customers_CustomerId" FOREIGN KEY ("CustomerId")
        REFERENCES "Customers" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_CustomerAddresses_CustomerId_IsDefault"
    ON "CustomerAddresses" ("CustomerId", "IsDefault");

CREATE TABLE IF NOT EXISTS "CustomerPaymentMethods" (
    "Id" uuid NOT NULL,
    "CustomerId" uuid NOT NULL,
    "Method" character varying(32) NOT NULL,
    "Label" character varying(120) NOT NULL,
    "MaskedReference" character varying(120),
    "IsDefault" boolean NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_CustomerPaymentMethods" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_CustomerPaymentMethods_Customers_CustomerId" FOREIGN KEY ("CustomerId")
        REFERENCES "Customers" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_CustomerPaymentMethods_CustomerId_IsDefault"
    ON "CustomerPaymentMethods" ("CustomerId", "IsDefault");
