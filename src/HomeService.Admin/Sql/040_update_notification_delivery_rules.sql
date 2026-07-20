CREATE TABLE IF NOT EXISTS "NotificationDeliveryRules" (
    "Id" uuid NOT NULL,
    "EventKey" character varying(96) NOT NULL,
    "Label" character varying(180) NOT NULL,
    "Audience" character varying(32) NOT NULL,
    "PortalEnabled" boolean NOT NULL DEFAULT false,
    "MobileAppEnabled" boolean NOT NULL DEFAULT false,
    "EmailEnabled" boolean NOT NULL DEFAULT false,
    "WhatsAppEnabled" boolean NOT NULL DEFAULT false,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
    "UpdatedAt" timestamp with time zone NULL,
    CONSTRAINT "PK_NotificationDeliveryRules" PRIMARY KEY ("Id")
);

ALTER TABLE "NotificationDeliveryRules"
    ADD COLUMN IF NOT EXISTS "EventKey" character varying(96) NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS "Label" character varying(180) NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS "Audience" character varying(32) NOT NULL DEFAULT 'Company',
    ADD COLUMN IF NOT EXISTS "PortalEnabled" boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS "MobileAppEnabled" boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS "EmailEnabled" boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS "WhatsAppEnabled" boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
    ADD COLUMN IF NOT EXISTS "UpdatedAt" timestamp with time zone NULL;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_NotificationDeliveryRules_EventKey"
    ON "NotificationDeliveryRules" ("EventKey");

CREATE INDEX IF NOT EXISTS "IX_NotificationDeliveryRules_Audience_EventKey"
    ON "NotificationDeliveryRules" ("Audience", "EventKey");

INSERT INTO "NotificationDeliveryRules"
    ("Id", "EventKey", "Label", "Audience", "PortalEnabled", "MobileAppEnabled", "EmailEnabled", "WhatsAppEnabled", "CreatedAt", "UpdatedAt")
VALUES
    ('3c8b462a-5d7f-43f2-9c32-720b0b5e2001', 'CompanyDocumentRejected', 'Piece entreprise refusee', 'Company', true, false, true, true, now(), now()),
    ('3c8b462a-5d7f-43f2-9c32-720b0b5e2002', 'CompanyDocumentNeedsReplacement', 'Complement requis sur dossier entreprise', 'Company', true, false, true, true, now(), now()),
    ('3c8b462a-5d7f-43f2-9c32-720b0b5e2003', 'CompanyDocumentReopened', 'Piece entreprise reouverte', 'Company', true, false, true, true, now(), now()),
    ('3c8b462a-5d7f-43f2-9c32-720b0b5e2004', 'CompanyApplicationRejected', 'Dossier entreprise refuse', 'Company', true, false, true, true, now(), now()),
    ('3c8b462a-5d7f-43f2-9c32-720b0b5e2005', 'CompanyApplicationReopened', 'Dossier entreprise reouvert', 'Company', true, false, true, true, now(), now()),
    ('3c8b462a-5d7f-43f2-9c32-720b0b5e2006', 'CompanyApplicationMoreInformationRequested', 'Complement requis sur dossier entreprise', 'Company', true, false, true, true, now(), now()),
    ('3c8b462a-5d7f-43f2-9c32-720b0b5e2007', 'CompanyApplicationApproved', 'Dossier entreprise valide', 'Company', true, false, true, true, now(), now()),
    ('3c8b462a-5d7f-43f2-9c32-720b0b5e2008', 'CompanyActivationLinkCreated', 'Lien d''activation entreprise', 'Company', true, false, true, true, now(), now()),
    ('3c8b462a-5d7f-43f2-9c32-720b0b5e2009', 'InterimCandidateReceived', 'Nouvelle demande interimaire', 'Company', true, false, false, false, now(), now()),
    ('3c8b462a-5d7f-43f2-9c32-720b0b5e2010', 'InterimCandidateApproved', 'Candidature interimaire acceptee', 'Provider', false, true, false, true, now(), now()),
    ('3c8b462a-5d7f-43f2-9c32-720b0b5e2011', 'MissionAssignedToProvider', 'Mission affectee au prestataire', 'Provider', false, true, false, true, now(), now()),
    ('3c8b462a-5d7f-43f2-9c32-720b0b5e2012', 'MissionQuoteSentToCustomer', 'Devis mission envoye au client', 'Customer', false, true, true, true, now(), now()),
    ('3c8b462a-5d7f-43f2-9c32-720b0b5e2013', 'MissionStatusChanged', 'Suivi de mission', 'Mixed', true, true, false, false, now(), now())
ON CONFLICT ("EventKey") DO UPDATE
SET "Label" = EXCLUDED."Label",
    "Audience" = EXCLUDED."Audience",
    "PortalEnabled" = CASE WHEN EXCLUDED."Audience" IN ('Company', 'Mixed') THEN true ELSE false END,
    "MobileAppEnabled" = CASE WHEN EXCLUDED."Audience" IN ('Provider', 'Customer', 'Mixed') THEN true ELSE false END,
    "UpdatedAt" = now();

UPDATE "NotificationDeliveryRules"
SET "PortalEnabled" = CASE WHEN "Audience" IN ('Company', 'Mixed') THEN true ELSE false END,
    "MobileAppEnabled" = CASE WHEN "Audience" IN ('Provider', 'Customer', 'Mixed') THEN true ELSE false END,
    "UpdatedAt" = now()
WHERE "PortalEnabled" <> CASE WHEN "Audience" IN ('Company', 'Mixed') THEN true ELSE false END
   OR "MobileAppEnabled" <> CASE WHEN "Audience" IN ('Provider', 'Customer', 'Mixed') THEN true ELSE false END;
