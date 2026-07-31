CREATE TABLE IF NOT EXISTS "MissionWorkflowSettings" (
    "Id" uuid NOT NULL,
    "Key" character varying(96) NOT NULL,
    "Label" character varying(180) NOT NULL,
    "Description" character varying(360) NOT NULL,
    "Unit" character varying(40) NOT NULL,
    "Value" integer NOT NULL,
    "MinimumValue" integer NOT NULL,
    "MaximumValue" integer NOT NULL,
    "SortOrder" integer NOT NULL,
    "IsActive" boolean NOT NULL DEFAULT true,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
    "UpdatedAt" timestamp with time zone NULL,
    CONSTRAINT "PK_MissionWorkflowSettings" PRIMARY KEY ("Id")
);

ALTER TABLE "MissionWorkflowSettings"
    ADD COLUMN IF NOT EXISTS "Key" character varying(96) NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS "Label" character varying(180) NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS "Description" character varying(360) NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS "Unit" character varying(40) NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS "Value" integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "MinimumValue" integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "MaximumValue" integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "SortOrder" integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "IsActive" boolean NOT NULL DEFAULT true,
    ADD COLUMN IF NOT EXISTS "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
    ADD COLUMN IF NOT EXISTS "UpdatedAt" timestamp with time zone NULL;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_MissionWorkflowSettings_Key"
    ON "MissionWorkflowSettings" ("Key");

CREATE INDEX IF NOT EXISTS "IX_MissionWorkflowSettings_IsActive_SortOrder"
    ON "MissionWorkflowSettings" ("IsActive", "SortOrder");

INSERT INTO "MissionWorkflowSettings"
    ("Id", "Key", "Label", "Description", "Unit", "Value", "MinimumValue", "MaximumValue", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
VALUES
    ('4b41a1fd-6e27-4d0a-a824-8d5d91470001', 'company_offer_response_minutes', 'Reponse entreprise', 'Temps laisse a une entreprise pour analyser une demande client et confirmer son interet avant relais.', 'minutes', 10, 1, 120, 10, true, now(), now()),
    ('4b41a1fd-6e27-4d0a-a824-8d5d91470002', 'urgent_company_offer_response_minutes', 'Reponse entreprise urgente', 'Temps laisse a une entreprise sur une demande urgente avant de proposer la mission a une autre entreprise.', 'minutes', 5, 1, 60, 20, true, now(), now()),
    ('4b41a1fd-6e27-4d0a-a824-8d5d91470003', 'provider_acceptance_minutes', 'Acceptation prestataire', 'Temps donne au prestataire pour accepter ou refuser une mission directe avant de passer a un autre prestataire.', 'minutes', 3, 1, 30, 30, true, now(), now()),
    ('4b41a1fd-6e27-4d0a-a824-8d5d91470004', 'scheduled_provider_acceptance_minutes', 'Acceptation rendez-vous', 'Temps donne au prestataire pour accepter une mission programmee ou un rendez-vous.', 'minutes', 30, 5, 240, 40, true, now(), now()),
    ('4b41a1fd-6e27-4d0a-a824-8d5d91470005', 'customer_quote_validity_minutes', 'Validite devis client', 'Temps pendant lequel le prix propose par l entreprise reste valable cote client.', 'minutes', 30, 5, 1440, 50, true, now(), now()),
    ('4b41a1fd-6e27-4d0a-a824-8d5d91470006', 'arrival_tolerance_meters', 'Tolerance arrivee GPS', 'Distance maximale acceptee entre le prestataire et le lieu client pour declarer l arrivee.', 'metres', 250, 25, 2000, 60, true, now(), now()),
    ('4b41a1fd-6e27-4d0a-a824-8d5d91470007', 'mission_start_grace_minutes', 'Marge debut mission', 'Retard tolerable apres l heure prevue avant signalement dans le suivi operationnel.', 'minutes', 15, 0, 120, 70, true, now(), now()),
    ('4b41a1fd-6e27-4d0a-a824-8d5d91470009', 'urgent_missions_enabled', 'Demandes urgentes', 'Autorise le client a demander une intervention urgente. Des frais supplementaires peuvent etre appliques.', 'boolean', 0, 0, 1, 80, true, now(), now())
ON CONFLICT ("Key") DO UPDATE
SET "Label" = EXCLUDED."Label",
    "Description" = EXCLUDED."Description",
    "Unit" = EXCLUDED."Unit",
    "MinimumValue" = EXCLUDED."MinimumValue",
    "MaximumValue" = EXCLUDED."MaximumValue",
    "SortOrder" = EXCLUDED."SortOrder",
    "IsActive" = true,
    "UpdatedAt" = now();
