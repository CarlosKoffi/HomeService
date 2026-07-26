CREATE TABLE IF NOT EXISTS "NotificationTemplates" (
    "Id" uuid NOT NULL,
    "EventKey" character varying(96) NOT NULL,
    "Channel" character varying(32) NOT NULL,
    "Label" character varying(180) NOT NULL,
    "Audience" character varying(32) NOT NULL,
    "SubjectTemplate" character varying(180) NOT NULL,
    "BodyTemplate" character varying(2000) NOT NULL,
    "AvailableVariables" character varying(1000),
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_NotificationTemplates" PRIMARY KEY ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_NotificationTemplates_Audience_Channel_IsActive"
    ON "NotificationTemplates" ("Audience", "Channel", "IsActive");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_NotificationTemplates_EventKey_Channel"
    ON "NotificationTemplates" ("EventKey", "Channel");

WITH seeds("Id", "EventKey", "Channel", "Label", "Audience", "SubjectTemplate", "BodyTemplate", "AvailableVariables") AS (
    VALUES
    ('4b8e8d8a-c2d0-4c72-8b41-f50200000001'::uuid, 'CompanyDocumentRejected', 'Portal', 'Piece entreprise refusee', 'Company', 'Piece a reprendre', '{NomEntreprise}, une piece de votre dossier demande une correction. {Note}', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}'),
    ('4b8e8d8a-c2d0-4c72-8b41-f50200000002'::uuid, 'CompanyDocumentRejected', 'Email', 'Piece entreprise refusee', 'Company', 'Piece a reprendre', '{NomEntreprise}, une piece de votre dossier demande une correction. {Note}', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}'),
    ('4b8e8d8a-c2d0-4c72-8b41-f50200000003'::uuid, 'CompanyDocumentRejected', 'WhatsApp', 'Piece entreprise refusee', 'Company', 'Piece a reprendre', '{NomEntreprise}, une piece de votre dossier demande une correction. {Note}', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}'),
    ('4b8e8d8a-c2d0-4c72-8b41-f50200000004'::uuid, 'CompanyDocumentNeedsReplacement', 'Portal', 'Complement requis sur dossier entreprise', 'Company', 'Complement requis', '{NomEntreprise}, notre equipe demande un complement sur votre dossier. {Note}', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}'),
    ('4b8e8d8a-c2d0-4c72-8b41-f50200000005'::uuid, 'CompanyDocumentNeedsReplacement', 'Email', 'Complement requis sur dossier entreprise', 'Company', 'Complement requis', '{NomEntreprise}, notre equipe demande un complement sur votre dossier. {Note}', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}'),
    ('4b8e8d8a-c2d0-4c72-8b41-f50200000006'::uuid, 'CompanyDocumentNeedsReplacement', 'WhatsApp', 'Complement requis sur dossier entreprise', 'Company', 'Complement requis', '{NomEntreprise}, notre equipe demande un complement sur votre dossier. {Note}', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}'),
    ('4b8e8d8a-c2d0-4c72-8b41-f50200000007'::uuid, 'CompanyApplicationApproved', 'Portal', 'Dossier entreprise valide', 'Company', 'Dossier valide', '{NomEntreprise}, votre entreprise est validee sur Wele.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}'),
    ('4b8e8d8a-c2d0-4c72-8b41-f50200000008'::uuid, 'CompanyApplicationApproved', 'Email', 'Dossier entreprise valide', 'Company', 'Dossier valide', '{NomEntreprise}, votre entreprise est validee sur Wele.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}'),
    ('4b8e8d8a-c2d0-4c72-8b41-f50200000009'::uuid, 'CompanyActivationLinkCreated', 'Email', 'Lien d''activation entreprise', 'Company', 'Activation de votre portail', '{NomEntreprise}, votre lien d''activation est pret: {LienAction}', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}'),
    ('4b8e8d8a-c2d0-4c72-8b41-f50200000010'::uuid, 'CompanyActivationLinkCreated', 'WhatsApp', 'Lien d''activation entreprise', 'Company', 'Activation de votre portail', '{NomEntreprise}, votre lien d''activation est pret: {LienAction}', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}'),
    ('4b8e8d8a-c2d0-4c72-8b41-f50200000011'::uuid, 'InterimCandidateReceived', 'Portal', 'Nouvelle demande interimaire', 'Company', 'Nouvelle candidature', '{NomEntreprise}, {NomPrestataire} souhaite collaborer avec vous.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}'),
    ('4b8e8d8a-c2d0-4c72-8b41-f50200000012'::uuid, 'InterimCandidateApproved', 'MobilePush', 'Candidature interimaire acceptee', 'Provider', 'Candidature acceptee', '{NomPrestataire}, {NomEntreprise} a accepte votre candidature.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}'),
    ('4b8e8d8a-c2d0-4c72-8b41-f50200000013'::uuid, 'InterimCandidateApproved', 'WhatsApp', 'Candidature interimaire acceptee', 'Provider', 'Candidature acceptee', '{NomPrestataire}, {NomEntreprise} a accepte votre candidature.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}'),
    ('4b8e8d8a-c2d0-4c72-8b41-f50200000014'::uuid, 'MissionAssignedToProvider', 'MobilePush', 'Mission affectee au prestataire', 'Provider', 'Nouvelle mission disponible', 'Mission {Service} a accepter avant la fin du delai.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}'),
    ('4b8e8d8a-c2d0-4c72-8b41-f50200000015'::uuid, 'MissionAssignedToProvider', 'WhatsApp', 'Mission affectee au prestataire', 'Provider', 'Nouvelle mission disponible', 'Mission {Service} a accepter avant la fin du delai.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}'),
    ('4b8e8d8a-c2d0-4c72-8b41-f50200000016'::uuid, 'MissionQuoteSentToCustomer', 'MobilePush', 'Devis mission envoye au client', 'Customer', 'Devis disponible', 'Votre devis pour {Service} est disponible.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}'),
    ('4b8e8d8a-c2d0-4c72-8b41-f50200000017'::uuid, 'MissionQuoteSentToCustomer', 'Email', 'Devis mission envoye au client', 'Customer', 'Devis disponible', 'Votre devis pour {Service} est disponible.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}'),
    ('4b8e8d8a-c2d0-4c72-8b41-f50200000018'::uuid, 'MissionQuoteSentToCustomer', 'WhatsApp', 'Devis mission envoye au client', 'Customer', 'Devis disponible', 'Votre devis pour {Service} est disponible.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}'),
    ('4b8e8d8a-c2d0-4c72-8b41-f50200000019'::uuid, 'MissionStatusChanged', 'Portal', 'Suivi de mission', 'Mixed', 'Suivi mission {NumeroMission}', 'La mission {NumeroMission} a ete mise a jour.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}'),
    ('4b8e8d8a-c2d0-4c72-8b41-f50200000020'::uuid, 'MissionStatusChanged', 'MobilePush', 'Suivi de mission', 'Mixed', 'Suivi mission {NumeroMission}', 'La mission {NumeroMission} a ete mise a jour.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}')
)
INSERT INTO "NotificationTemplates"
    ("Id", "EventKey", "Channel", "Label", "Audience", "SubjectTemplate", "BodyTemplate", "AvailableVariables", "IsActive", "CreatedAt", "UpdatedAt")
SELECT "Id", "EventKey", "Channel", "Label", "Audience", "SubjectTemplate", "BodyTemplate", "AvailableVariables", true, now(), now()
FROM seeds
ON CONFLICT ("EventKey", "Channel") DO UPDATE
SET
    "Label" = EXCLUDED."Label",
    "Audience" = EXCLUDED."Audience",
    "AvailableVariables" = EXCLUDED."AvailableVariables",
    "UpdatedAt" = now();

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260726010903_AddNotificationTemplates', '9.0.0'
WHERE NOT EXISTS (
    SELECT 1
    FROM "__EFMigrationsHistory"
    WHERE "MigrationId" = '20260726010903_AddNotificationTemplates'
);
