using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClientNotificationInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "NotificationOutboxMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerType",
                table: "NotificationOutboxMessages",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReadAt",
                table: "NotificationOutboxMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutboxMessages_OwnerType_OwnerId_ReadAt",
                table: "NotificationOutboxMessages",
                columns: new[] { "OwnerType", "OwnerId", "ReadAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationOutboxMessages_OwnerType_OwnerId_ReadAt",
                table: "NotificationOutboxMessages");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "NotificationOutboxMessages");

            migrationBuilder.DropColumn(
                name: "OwnerType",
                table: "NotificationOutboxMessages");

            migrationBuilder.DropColumn(
                name: "ReadAt",
                table: "NotificationOutboxMessages");
        }
    }
}
