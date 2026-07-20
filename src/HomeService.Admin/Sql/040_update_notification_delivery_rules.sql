CREATE TABLE IF NOT EXISTS "NotificationDeliveryRules" (
    "Id" uuid NOT NULL,
    "EventKey" character varying(96) NOT NULL,
    "Label" character varying(180) NOT NULL,
    "Audience" character varying(32) NOT NULL,
    "PortalEnabled" boolean NOT NULL,
    "MobileAppEnabled" boolean NOT NULL,
    "EmailEnabled" boolean NOT NULL,
    "WhatsAppEnabled" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NULL,
    CONSTRAINT "PK_NotificationDeliveryRules" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_NotificationDeliveryRules_EventKey"
    ON "NotificationDeliveryRules" ("EventKey");

CREATE INDEX IF NOT EXISTS "IX_NotificationDeliveryRules_Audience_EventKey"
    ON "NotificationDeliveryRules" ("Audience", "EventKey");

INSERT INTO "NotificationDeliveryRules"
    ("Id", "EventKey", "Label", "Audience", "PortalEnabled", "MobileAppEnabled", "EmailEnabled", "WhatsAppEnabled", "CreatedAt", "UpdatedAt")
VALUES
    ('3c8b462a-5d7f-43f2-9c32-720b0b5e2010', 'CompanyDocumentReopened', 'Piece entreprise reouverte', 'Company', true, false, true, true, now(), now()),
    ('3c8b462a-5d7f-43f2-9c32-720b0b5e2011', 'CompanyApplicationRejected', 'Dossier entreprise refuse', 'Company', true, false, true, true, now(), now()),
    ('3c8b462a-5d7f-43f2-9c32-720b0b5e2012', 'CompanyApplicationReopened', 'Dossier entreprise reouvert', 'Company', true, false, true, true, now(), now()),
    ('3c8b462a-5d7f-43f2-9c32-720b0b5e2013', 'CompanyApplicationMoreInformationRequested', 'Complement requis sur dossier entreprise', 'Company', true, false, true, true, now(), now())
ON CONFLICT ("EventKey") DO UPDATE
SET "Label" = EXCLUDED."Label",
    "Audience" = EXCLUDED."Audience",
    "PortalEnabled" = true,
    "MobileAppEnabled" = false,
    "EmailEnabled" = EXCLUDED."EmailEnabled",
    "WhatsAppEnabled" = EXCLUDED."WhatsAppEnabled",
    "UpdatedAt" = now();

UPDATE "NotificationDeliveryRules"
SET "PortalEnabled" = CASE WHEN "Audience" IN ('Company', 'Mixed') THEN true ELSE false END,
    "MobileAppEnabled" = CASE WHEN "Audience" IN ('Provider', 'Customer', 'Mixed') THEN true ELSE false END,
    "UpdatedAt" = now()
WHERE "PortalEnabled" <> CASE WHEN "Audience" IN ('Company', 'Mixed') THEN true ELSE false END
   OR "MobileAppEnabled" <> CASE WHEN "Audience" IN ('Provider', 'Customer', 'Mixed') THEN true ELSE false END;
