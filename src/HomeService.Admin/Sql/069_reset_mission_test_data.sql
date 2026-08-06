-- One-time mission reset for the real mobile workflow test campaign.
-- The matching EF Core migration is applied automatically once by the API
-- during deployment. Accounts, companies, providers, catalog, settings,
-- payment methods and Firebase device tokens are preserved.
--
-- Destructive and intentionally irreversible. Run inside a transaction.

CREATE TEMP TABLE "WeleMissionResetIds" (
    "Id" uuid PRIMARY KEY
) ON COMMIT DROP;

INSERT INTO "WeleMissionResetIds" ("Id")
SELECT "Id" FROM "Missions";

CREATE TEMP TABLE "WeleMissionResetProviderIds" (
    "Id" uuid PRIMARY KEY
) ON COMMIT DROP;

INSERT INTO "WeleMissionResetProviderIds" ("Id")
SELECT "ProviderId" FROM "Missions" WHERE "ProviderId" IS NOT NULL
UNION
SELECT "ProviderId" FROM "ProviderMissionAssignments";

CREATE TEMP TABLE "WeleMissionResetRelatedIds" (
    "Id" uuid PRIMARY KEY
) ON COMMIT DROP;

INSERT INTO "WeleMissionResetRelatedIds" ("Id")
SELECT "Id" FROM "WeleMissionResetIds"
UNION SELECT "Id" FROM "MissionConversations"
UNION SELECT "Id" FROM "ProviderMissionAssignments"
UNION SELECT "Id" FROM "MissionDispatchOffers"
UNION SELECT "Id" FROM "MissionAdditionalQuotes"
UNION SELECT "Id" FROM "MissionDisputes";

DELETE FROM "NotificationOutboxMessages"
WHERE "RelatedEntityId" IN (
    SELECT "Id" FROM "WeleMissionResetRelatedIds"
);

DELETE FROM "CompanyPortalActivities"
WHERE "EntityId" IN (
    SELECT "Id" FROM "WeleMissionResetRelatedIds"
)
   OR "EntityType" IN (
       'Mission',
       'MissionConversation',
       'MissionDispatchOffer',
       'ProviderMissionAssignment',
       'MissionAdditionalQuote',
       'MissionDispute'
   );

DELETE FROM "AuditLogEntries"
WHERE "EntityId" IN (
    SELECT "Id" FROM "WeleMissionResetRelatedIds"
)
   OR "EntityType" IN (
       'Mission',
       'MissionConversation',
       'MissionDispatchOffer',
       'ProviderMissionAssignment',
       'MissionAdditionalQuote',
       'MissionDispute'
   );

DELETE FROM "CompanyPortalNotifications"
WHERE COALESCE("ActionUrl", '') ILIKE '%/missions/%'
   OR "Type" ILIKE 'Mission%';

UPDATE "Providers"
SET "IsAvailable" = TRUE,
    "MissionLatitude" = NULL,
    "MissionLongitude" = NULL,
    "UpdatedAt" = now()
WHERE "Id" IN (
    SELECT "Id" FROM "WeleMissionResetProviderIds"
);

TRUNCATE TABLE "Missions" CASCADE;
