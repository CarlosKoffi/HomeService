using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class LinkNotificationTemplatesToRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                WITH missing_rules AS (
                    SELECT
                        template."EventKey",
                        max(template."Label") AS "Label",
                        max(template."Audience") AS "Audience",
                        bool_or(template."Channel" = 'Email') AS "EmailEnabled",
                        bool_or(template."Channel" = 'WhatsApp') AS "WhatsAppEnabled",
                        max(template."SubjectTemplate") AS "SubjectTemplate",
                        max(template."BodyTemplate") AS "BodyTemplate"
                    FROM "NotificationTemplates" AS template
                    LEFT JOIN "NotificationDeliveryRules" AS rule ON rule."EventKey" = template."EventKey"
                    WHERE rule."Id" IS NULL
                    GROUP BY template."EventKey"
                ),
                missing_rules_with_id AS (
                    SELECT
                        (
                            substr(md5("EventKey"), 1, 8) || '-' ||
                            substr(md5("EventKey"), 9, 4) || '-' ||
                            substr(md5("EventKey"), 13, 4) || '-' ||
                            substr(md5("EventKey"), 17, 4) || '-' ||
                            substr(md5("EventKey"), 21, 12)
                        )::uuid AS "Id",
                        *
                    FROM missing_rules
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
                FROM missing_rules_with_id
                ON CONFLICT ("EventKey") DO NOTHING;
                """);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_NotificationDeliveryRules_EventKey",
                table: "NotificationDeliveryRules",
                column: "EventKey");

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationTemplates_NotificationDeliveryRules_EventKey",
                table: "NotificationTemplates",
                column: "EventKey",
                principalTable: "NotificationDeliveryRules",
                principalColumn: "EventKey",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NotificationTemplates_NotificationDeliveryRules_EventKey",
                table: "NotificationTemplates");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_NotificationDeliveryRules_EventKey",
                table: "NotificationDeliveryRules");
        }
    }
}
