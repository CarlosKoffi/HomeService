ALTER TABLE "NotificationDeliveryRules"
    ADD COLUMN IF NOT EXISTS "SubjectTemplate" character varying(180);

ALTER TABLE "NotificationDeliveryRules"
    ADD COLUMN IF NOT EXISTS "BodyTemplate" character varying(2000);

UPDATE "NotificationDeliveryRules"
SET
    "SubjectTemplate" = CASE "EventKey"
        WHEN 'CompanyDocumentRejected' THEN 'Piece a reprendre'
        WHEN 'CompanyDocumentNeedsReplacement' THEN 'Complement requis'
        WHEN 'CompanyDocumentReopened' THEN 'Piece reouverte'
        WHEN 'CompanyApplicationRejected' THEN 'Dossier refuse'
        WHEN 'CompanyApplicationReopened' THEN 'Dossier reouvert'
        WHEN 'CompanyApplicationMoreInformationRequested' THEN 'Complement requis'
        WHEN 'CompanyApplicationApproved' THEN 'Dossier valide'
        WHEN 'CompanyActivationLinkCreated' THEN 'Activation de votre portail'
        WHEN 'InterimCandidateReceived' THEN 'Nouvelle candidature'
        WHEN 'InterimCandidateApproved' THEN 'Candidature acceptee'
        WHEN 'MissionAssignedToProvider' THEN 'Nouvelle mission disponible'
        WHEN 'MissionQuoteSentToCustomer' THEN 'Devis disponible'
        WHEN 'MissionStatusChanged' THEN 'Suivi mission {NumeroMission}'
        ELSE COALESCE("SubjectTemplate", "Label")
    END,
    "BodyTemplate" = CASE "EventKey"
        WHEN 'CompanyDocumentRejected' THEN '{NomEntreprise}, une piece de votre dossier demande une correction.'
        WHEN 'CompanyDocumentNeedsReplacement' THEN '{NomEntreprise}, notre equipe demande un complement sur votre dossier.'
        WHEN 'CompanyDocumentReopened' THEN '{NomEntreprise}, une piece de votre dossier a ete remise en verification.'
        WHEN 'CompanyApplicationRejected' THEN '{NomEntreprise}, votre demande partenaire n''a pas pu etre validee pour le moment.'
        WHEN 'CompanyApplicationReopened' THEN '{NomEntreprise}, votre dossier partenaire est de nouveau en analyse.'
        WHEN 'CompanyApplicationMoreInformationRequested' THEN '{NomEntreprise}, un complement est necessaire pour terminer l''analyse.'
        WHEN 'CompanyApplicationApproved' THEN '{NomEntreprise}, votre entreprise est validee sur Wele.'
        WHEN 'CompanyActivationLinkCreated' THEN '{NomEntreprise}, votre lien d''activation est pret.'
        WHEN 'InterimCandidateReceived' THEN '{NomEntreprise}, {NomPrestataire} souhaite collaborer avec vous.'
        WHEN 'InterimCandidateApproved' THEN '{NomPrestataire}, {NomEntreprise} a accepte votre candidature.'
        WHEN 'MissionAssignedToProvider' THEN 'Mission {Service} a accepter avant la fin du delai.'
        WHEN 'MissionQuoteSentToCustomer' THEN 'Votre devis pour {Service} est disponible.'
        WHEN 'MissionStatusChanged' THEN 'La mission {NumeroMission} a ete mise a jour.'
        ELSE COALESCE("BodyTemplate", "Label")
    END
WHERE "SubjectTemplate" IS NULL
   OR "BodyTemplate" IS NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260726004934_AddNotificationDeliveryTemplates', '9.0.0'
WHERE NOT EXISTS (
    SELECT 1
    FROM "__EFMigrationsHistory"
    WHERE "MigrationId" = '20260726004934_AddNotificationDeliveryTemplates'
);
