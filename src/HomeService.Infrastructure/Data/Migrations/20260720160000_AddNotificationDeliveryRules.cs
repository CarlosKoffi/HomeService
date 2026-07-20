using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    public partial class AddNotificationDeliveryRules : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
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

                CREATE INDEX IF NOT EXISTS "IX_NotificationDeliveryRules_Audience_EventKey"
                    ON "NotificationDeliveryRules" ("Audience", "EventKey");

                CREATE UNIQUE INDEX IF NOT EXISTS "IX_NotificationDeliveryRules_EventKey"
                    ON "NotificationDeliveryRules" ("EventKey");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationDeliveryRules");
        }
    }
}
