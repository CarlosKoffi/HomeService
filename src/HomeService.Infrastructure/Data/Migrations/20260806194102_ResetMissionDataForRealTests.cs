using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ResetMissionDataForRealTests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
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
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Mission data cannot be reconstructed after this intentional reset.
        }
    }
}
