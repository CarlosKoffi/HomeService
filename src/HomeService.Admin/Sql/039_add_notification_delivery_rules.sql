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

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260720160000_AddNotificationDeliveryRules', '9.0.0'
WHERE NOT EXISTS (
    SELECT 1
    FROM "__EFMigrationsHistory"
    WHERE "MigrationId" = '20260720160000_AddNotificationDeliveryRules'
);

INSERT INTO "NotificationDeliveryRules"
    ("Id", "EventKey", "Label", "Audience", "PortalEnabled", "MobileAppEnabled", "EmailEnabled", "WhatsAppEnabled", "CreatedAt")
VALUES
    ('3c8b462a-5d7f-43f2-9c32-720b0b5e2001', 'CompanyDocumentRejected', 'Piece entreprise refusee', 'Company', true, false, true, true, now()),
    ('3c8b462a-5d7f-43f2-9c32-720b0b5e2002', 'CompanyDocumentNeedsReplacement', 'Complement requis sur dossier entreprise', 'Company', true, false, true, true, now()),
    ('3c8b462a-5d7f-43f2-9c32-720b0b5e2003', 'CompanyApplicationApproved', 'Dossier entreprise valide', 'Company', true, false, true, true, now()),
    ('3c8b462a-5d7f-43f2-9c32-720b0b5e2004', 'CompanyActivationLinkCreated', 'Lien d''activation entreprise', 'Company', true, false, true, true, now()),
    ('3c8b462a-5d7f-43f2-9c32-720b0b5e2005', 'InterimCandidateReceived', 'Nouvelle demande interimaire', 'Company', true, false, false, false, now()),
    ('3c8b462a-5d7f-43f2-9c32-720b0b5e2006', 'InterimCandidateApproved', 'Candidature interimaire acceptee', 'Provider', false, true, false, true, now()),
    ('3c8b462a-5d7f-43f2-9c32-720b0b5e2007', 'MissionAssignedToProvider', 'Mission affectee au prestataire', 'Provider', false, true, false, true, now()),
    ('3c8b462a-5d7f-43f2-9c32-720b0b5e2008', 'MissionQuoteSentToCustomer', 'Devis mission envoye au client', 'Customer', false, true, true, true, now()),
    ('3c8b462a-5d7f-43f2-9c32-720b0b5e2009', 'MissionStatusChanged', 'Suivi de mission', 'Mixed', true, true, false, false, now())
ON CONFLICT ("EventKey") DO NOTHING;
