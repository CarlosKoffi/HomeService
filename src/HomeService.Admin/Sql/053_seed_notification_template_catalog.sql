CREATE TABLE IF NOT EXISTS "NotificationTemplates" (
    "Id" uuid NOT NULL,
    "EventKey" character varying(96) NOT NULL,
    "Channel" character varying(32) NOT NULL,
    "Label" character varying(180) NOT NULL,
    "Audience" character varying(32) NOT NULL,
    "SubjectTemplate" character varying(180) NOT NULL,
    "BodyTemplate" character varying(2000) NOT NULL,
    "AvailableVariables" character varying(1000),
    "IsActive" boolean NOT NULL DEFAULT true,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_NotificationTemplates" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_NotificationTemplates_EventKey_Channel"
    ON "NotificationTemplates" ("EventKey", "Channel");

CREATE INDEX IF NOT EXISTS "IX_NotificationTemplates_Audience_Channel_IsActive"
    ON "NotificationTemplates" ("Audience", "Channel", "IsActive");

WITH seeds("EventKey", "Audience", "Channels", "Label", "SubjectTemplate", "BodyTemplate", "AvailableVariables") AS (
    VALUES
    ('CompanyDocumentRejected', 'Company', ARRAY['Portal','Email','WhatsApp'], 'Piece entreprise refusee', 'Piece a reprendre', '{NomEntreprise}, une piece de votre dossier demande une correction. {Note}', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('CompanyDocumentNeedsReplacement', 'Company', ARRAY['Portal','Email','WhatsApp'], 'Complement requis sur dossier entreprise', 'Complement requis', '{NomEntreprise}, notre equipe demande un complement sur votre dossier. {Note}', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('CompanyDocumentReopened', 'Company', ARRAY['Portal','Email','WhatsApp'], 'Piece entreprise reouverte', 'Piece reouverte', '{NomEntreprise}, une piece de votre dossier a ete remise en verification.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('CompanyApplicationRejected', 'Company', ARRAY['Portal','Email','WhatsApp'], 'Dossier entreprise refuse', 'Dossier refuse', '{NomEntreprise}, votre demande partenaire n''a pas pu etre validee pour le moment. {Note}', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('CompanyApplicationReopened', 'Company', ARRAY['Portal','Email','WhatsApp'], 'Dossier entreprise reouvert', 'Dossier reouvert', '{NomEntreprise}, votre dossier partenaire est de nouveau en analyse.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('CompanyApplicationMoreInformationRequested', 'Company', ARRAY['Portal','Email','WhatsApp'], 'Complement requis sur dossier entreprise', 'Complement requis', '{NomEntreprise}, un complement est necessaire pour terminer l''analyse. {Note}', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('CompanyApplicationApproved', 'Company', ARRAY['Portal','Email','WhatsApp'], 'Dossier entreprise valide', 'Dossier valide', '{NomEntreprise}, votre entreprise est validee sur Wele.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('CompanyActivationLinkCreated', 'Company', ARRAY['Portal','Email','WhatsApp'], 'Lien d''activation entreprise', 'Activation de votre portail', '{NomEntreprise}, votre lien d''activation est pret: {LienAction}', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('InterimCandidateReceived', 'Company', ARRAY['Portal','Email','WhatsApp'], 'Nouvelle demande interimaire', 'Nouvelle candidature', '{NomEntreprise}, {NomPrestataire} souhaite collaborer avec vous.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('MissionRequestReceived', 'Company', ARRAY['Portal','Email','WhatsApp'], 'Nouvelle demande client', 'Nouvelle demande client', '{NomEntreprise}, une demande {Service} est disponible dans votre zone.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('MissionQuoteRequired', 'Company', ARRAY['Portal','Email','WhatsApp'], 'Devis a preparer', 'Devis attendu', '{NomEntreprise}, analysez la demande {NumeroMission} et proposez votre prix.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('MissionQuoteAcceptedByCustomer', 'Company', ARRAY['Portal','Email','WhatsApp'], 'Devis accepte par le client', 'Devis accepte', 'Le client a accepte le devis de la mission {NumeroMission}. Vous pouvez affecter un technicien.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('MissionAssignmentDeadlineExpired', 'Company', ARRAY['Portal','Email','WhatsApp'], 'Delai d''affectation expire', 'Delai depasse', 'Le delai d''affectation de la mission {NumeroMission} est depasse.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('MissionProviderRefused', 'Company', ARRAY['Portal','Email','WhatsApp'], 'Prestataire a refuse', 'Mission refusee', '{NomPrestataire} a refuse la mission {NumeroMission}. {Motif}', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('MissionProviderAccepted', 'Company', ARRAY['Portal','Email','WhatsApp'], 'Prestataire a accepte', 'Mission acceptee', '{NomPrestataire} a accepte la mission {NumeroMission}.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('MissionAdditionalQuoteRequested', 'Company', ARRAY['Portal','Email','WhatsApp'], 'Devis complementaire demande', 'Complement demande', '{NomPrestataire} demande un devis complementaire pour {NumeroMission}. {Note}', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('MissionDisputeOpened', 'Company', ARRAY['Portal','Email','WhatsApp'], 'Litige mission ouvert', 'Litige ouvert', 'Un litige est ouvert sur la mission {NumeroMission}. {Motif}', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('MissionPaymentReleased', 'Company', ARRAY['Portal','Email','WhatsApp'], 'Paiement transfere', 'Paiement transfere', 'Le paiement de {Montant} pour {NumeroMission} est pret pour transfert.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('InterimCandidateApproved', 'Provider', ARRAY['MobilePush','Email','WhatsApp'], 'Candidature interimaire acceptee', 'Candidature acceptee', '{NomPrestataire}, {NomEntreprise} a accepte votre candidature.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('InterimCandidateRejected', 'Provider', ARRAY['MobilePush','Email','WhatsApp'], 'Candidature interimaire refusee', 'Candidature non retenue', '{NomPrestataire}, votre candidature chez {NomEntreprise} n''a pas ete retenue. {Note}', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('MissionAssignedToProvider', 'Provider', ARRAY['MobilePush','Email','WhatsApp'], 'Mission affectee au prestataire', 'Nouvelle mission disponible', 'Mission {Service} a accepter avant la fin du delai.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('MissionProviderAcceptanceReminder', 'Provider', ARRAY['MobilePush','Email','WhatsApp'], 'Rappel acceptation mission', 'Reponse attendue', '{NomPrestataire}, vous avez encore {Delai} pour repondre a la mission {NumeroMission}.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('MissionClientConfirmed', 'Provider', ARRAY['MobilePush','Email','WhatsApp'], 'Client a confirme', 'Mission confirmee', 'Le client a confirme la mission {NumeroMission}. Preparez votre intervention.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('MissionTechnicianCanStart', 'Provider', ARRAY['MobilePush','Email','WhatsApp'], 'Debut mission autorise', 'Vous pouvez demarrer', 'Vous pouvez demarrer la mission {NumeroMission} a l''adresse {Adresse}.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('MissionAdditionalQuoteApproved', 'Provider', ARRAY['MobilePush','Email','WhatsApp'], 'Devis complementaire accepte', 'Complement accepte', 'Le client a accepte le devis complementaire pour {NumeroMission}.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('MissionAdditionalQuoteRejected', 'Provider', ARRAY['MobilePush','Email','WhatsApp'], 'Devis complementaire refuse', 'Complement refuse', 'Le client a refuse le devis complementaire pour {NumeroMission}.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('ProviderProfileValidated', 'Provider', ARRAY['MobilePush','Email','WhatsApp'], 'Profil prestataire valide', 'Profil valide', '{NomPrestataire}, votre profil est valide pour recevoir des missions.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('ProviderProfileSuspended', 'Provider', ARRAY['MobilePush','Email','WhatsApp'], 'Profil prestataire suspendu', 'Profil suspendu', '{NomPrestataire}, votre acces mission est suspendu. {Motif}', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('MissionQuoteSentToCustomer', 'Customer', ARRAY['MobilePush','Email','WhatsApp'], 'Devis mission envoye au client', 'Devis disponible', 'Votre devis pour {Service} est disponible.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('MissionQuoteAccepted', 'Customer', ARRAY['MobilePush','Email','WhatsApp'], 'Paiement client recu', 'Paiement recu', 'Votre paiement pour la mission {NumeroMission} est confirme.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('MissionTechnicianAssigned', 'Customer', ARRAY['MobilePush','Email','WhatsApp'], 'Technicien affecte', 'Technicien affecte', '{NomTechnicien} interviendra pour votre mission {NumeroMission}.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('MissionTechnicianOnTheWay', 'Customer', ARRAY['MobilePush','Email','WhatsApp'], 'Technicien en route', 'Technicien en route', '{NomTechnicien} est en route vers {Adresse}.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('MissionTechnicianArrived', 'Customer', ARRAY['MobilePush','Email','WhatsApp'], 'Technicien arrive', 'Technicien arrive', '{NomTechnicien} est arrive pour la mission {NumeroMission}.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('MissionStarted', 'Customer', ARRAY['MobilePush','Email','WhatsApp'], 'Mission demarree', 'Mission demarree', 'La mission {NumeroMission} a demarre.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('MissionCompleted', 'Customer', ARRAY['MobilePush','Email','WhatsApp'], 'Mission terminee', 'Mission terminee', 'La mission {NumeroMission} est terminee. Vous pouvez valider et noter l''intervention.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('MissionReviewRequested', 'Customer', ARRAY['MobilePush','Email','WhatsApp'], 'Avis client demande', 'Votre avis compte', 'Notez la mission {NumeroMission}: qualite, ponctualite, politesse et proprete.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('MissionCancelled', 'Customer', ARRAY['MobilePush','Email','WhatsApp'], 'Mission annulee', 'Mission annulee', 'La mission {NumeroMission} a ete annulee. {Motif}', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('MissionRefundApproved', 'Customer', ARRAY['MobilePush','Email','WhatsApp'], 'Remboursement valide', 'Remboursement valide', 'Un remboursement de {Montant} est valide pour la mission {NumeroMission}.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}'),
    ('MissionStatusChanged', 'Mixed', ARRAY['Portal','MobilePush','Email','WhatsApp'], 'Suivi de mission', 'Suivi mission {NumeroMission}', 'La mission {NumeroMission} a ete mise a jour.', '{NomEntreprise}, {NomPrestataire}, {NomClient}, {Service}, {Prestation}, {DescriptionService}, {NumeroMission}, {Montant}, {DateMission}, {LienAction}, {Note}, {Motif}, {Delai}, {Adresse}, {NomTechnicien}')
),
expanded AS (
    SELECT
        "EventKey",
        "Audience",
        unnest("Channels") AS "Channel",
        "Label",
        "SubjectTemplate",
        "BodyTemplate",
        "AvailableVariables"
    FROM seeds
),
expanded_with_id AS (
    SELECT
        (
            substr(md5("EventKey" || '|' || "Channel"), 1, 8) || '-' ||
            substr(md5("EventKey" || '|' || "Channel"), 9, 4) || '-' ||
            substr(md5("EventKey" || '|' || "Channel"), 13, 4) || '-' ||
            substr(md5("EventKey" || '|' || "Channel"), 17, 4) || '-' ||
            substr(md5("EventKey" || '|' || "Channel"), 21, 12)
        )::uuid AS "Id",
        *
    FROM expanded
)
INSERT INTO "NotificationTemplates"
    ("Id", "EventKey", "Channel", "Label", "Audience", "SubjectTemplate", "BodyTemplate", "AvailableVariables", "IsActive", "CreatedAt", "UpdatedAt")
SELECT "Id", "EventKey", "Channel", "Label", "Audience", "SubjectTemplate", "BodyTemplate", "AvailableVariables", true, now(), now()
FROM expanded_with_id
ON CONFLICT ("EventKey", "Channel") DO UPDATE
SET
    "Label" = EXCLUDED."Label",
    "Audience" = EXCLUDED."Audience",
    "AvailableVariables" = EXCLUDED."AvailableVariables",
    "UpdatedAt" = now();

WITH event_rules AS (
    SELECT
        "EventKey",
        max("Label") AS "Label",
        max("Audience") AS "Audience",
        bool_or("Channel" = 'Email') AS "EmailEnabled",
        bool_or("Channel" = 'WhatsApp') AS "WhatsAppEnabled",
        max("SubjectTemplate") AS "SubjectTemplate",
        max("BodyTemplate") AS "BodyTemplate"
    FROM "NotificationTemplates"
    GROUP BY "EventKey"
),
event_rules_with_id AS (
    SELECT
        (
            substr(md5("EventKey"), 1, 8) || '-' ||
            substr(md5("EventKey"), 9, 4) || '-' ||
            substr(md5("EventKey"), 13, 4) || '-' ||
            substr(md5("EventKey"), 17, 4) || '-' ||
            substr(md5("EventKey"), 21, 12)
        )::uuid AS "Id",
        *
    FROM event_rules
)
INSERT INTO "NotificationDeliveryRules"
    ("Id", "EventKey", "Label", "Audience", "PortalEnabled", "MobileAppEnabled", "EmailEnabled", "WhatsAppEnabled", "SubjectTemplate", "BodyTemplate", "CreatedAt", "UpdatedAt")
SELECT
    "Id",
    "EventKey",
    "Label",
    "Audience",
    "Audience" IN ('Company', 'Mixed'),
    "Audience" IN ('Provider', 'Customer', 'Mixed'),
    "EmailEnabled",
    "WhatsAppEnabled",
    "SubjectTemplate",
    "BodyTemplate",
    now(),
    now()
FROM event_rules_with_id
ON CONFLICT ("EventKey") DO NOTHING;
