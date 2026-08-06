-- Nettoyage ciblé des données de missions pour une nouvelle campagne de tests.
-- Conserve les comptes, entreprises, prestataires, catalogues, moyens de paiement
-- enregistrés, paramètres métier et jetons Firebase.
--
-- À exécuter uniquement sur l'environnement de test souhaité. La transaction est
-- atomique : la moindre erreur annule l'intégralité du nettoyage.

BEGIN;

CREATE TEMP TABLE clean_mission_ids ("Id" uuid PRIMARY KEY) ON COMMIT DROP;
INSERT INTO clean_mission_ids ("Id")
SELECT "Id" FROM "Missions";

CREATE TEMP TABLE clean_provider_ids ("Id" uuid PRIMARY KEY) ON COMMIT DROP;
INSERT INTO clean_provider_ids ("Id")
SELECT "ProviderId" FROM "Missions" WHERE "ProviderId" IS NOT NULL
UNION
SELECT "ProviderId" FROM "ProviderMissionAssignments";

CREATE TEMP TABLE clean_related_ids ("Id" uuid PRIMARY KEY) ON COMMIT DROP;
INSERT INTO clean_related_ids ("Id")
SELECT "Id" FROM clean_mission_ids
UNION SELECT "Id" FROM "MissionConversations"
UNION SELECT "Id" FROM "ProviderMissionAssignments"
UNION SELECT "Id" FROM "MissionDispatchOffers"
UNION SELECT "Id" FROM "MissionAdditionalQuotes"
UNION SELECT "Id" FROM "MissionDisputes";

-- Retire les alertes et traces opérationnelles liées aux anciennes missions,
-- sans toucher aux notifications de compte, conformité ou sécurité.
DELETE FROM "NotificationOutboxMessages"
WHERE "RelatedEntityId" IN (SELECT "Id" FROM clean_related_ids);

DELETE FROM "CompanyPortalActivities"
WHERE "EntityId" IN (SELECT "Id" FROM clean_related_ids)
   OR "EntityType" IN (
       'Mission',
       'MissionConversation',
       'MissionDispatchOffer',
       'ProviderMissionAssignment',
       'MissionAdditionalQuote',
       'MissionDispute');

DELETE FROM "AuditLogEntries"
WHERE "EntityId" IN (SELECT "Id" FROM clean_related_ids)
   OR "EntityType" IN (
       'Mission',
       'MissionConversation',
       'MissionDispatchOffer',
       'ProviderMissionAssignment',
       'MissionAdditionalQuote',
       'MissionDispute');

DELETE FROM "CompanyPortalNotifications"
WHERE COALESCE("ActionUrl", '') ILIKE '%/missions/%'
   OR "Type" ILIKE 'Mission%';

-- Libère uniquement les prestataires qui étaient engagés dans une mission effacée.
UPDATE "Providers"
SET "IsAvailable" = TRUE,
    "MissionLatitude" = NULL,
    "MissionLongitude" = NULL
WHERE "Id" IN (SELECT "Id" FROM clean_provider_ids);

-- PostgreSQL supprime aussi toutes les tables filles liées par clé étrangère :
-- affectations, offres, conversations/messages, paiements, avis, pièces et historiques.
TRUNCATE TABLE "Missions" CASCADE;

COMMIT;

SELECT
    (SELECT COUNT(*) FROM "Missions") AS "RemainingMissions",
    (SELECT COUNT(*) FROM "ProviderMissionAssignments") AS "RemainingAssignments",
    (SELECT COUNT(*) FROM "MissionDispatchOffers") AS "RemainingOffers",
    (SELECT COUNT(*) FROM "MissionMessages") AS "RemainingMessages";
