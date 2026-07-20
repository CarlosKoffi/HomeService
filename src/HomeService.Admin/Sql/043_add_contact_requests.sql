CREATE TABLE IF NOT EXISTS "ContactRequests" (
    "Id" uuid NOT NULL,
    "Source" character varying(40) NOT NULL,
    "Status" character varying(40) NOT NULL,
    "FullName" character varying(180) NOT NULL,
    "CompanyName" character varying(180) NULL,
    "PhoneNumber" character varying(80) NOT NULL,
    "Email" character varying(220) NULL,
    "Subject" character varying(180) NOT NULL,
    "Message" character varying(2000) NOT NULL,
    "AdminNote" character varying(1000) NULL,
    "ProcessedAt" timestamp with time zone NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NULL,
    CONSTRAINT "PK_ContactRequests" PRIMARY KEY ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_ContactRequests_Source"
    ON "ContactRequests" ("Source");

CREATE INDEX IF NOT EXISTS "IX_ContactRequests_Status_CreatedAt"
    ON "ContactRequests" ("Status", "CreatedAt");
