using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMissionCancellationDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationComment",
                table: "Missions",
                type: "character varying(1200)",
                maxLength: 1200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "Missions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAt",
                table: "Missions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelledBy",
                table: "Missions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RefundAmount",
                table: "Missions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Missions_CancelledAt_CancelledBy",
                table: "Missions",
                columns: new[] { "CancelledAt", "CancelledBy" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Missions_CancelledAt_CancelledBy",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "CancellationComment",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "CancelledBy",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "RefundAmount",
                table: "Missions");
        }
    }
}
