using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClientMissionDeadlines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CustomerCompletionValidationExpiresAt",
                table: "Missions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CustomerPaymentExpiresAt",
                table: "Missions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Missions_CustomerCompletionValidationExpiresAt_Status",
                table: "Missions",
                columns: new[] { "CustomerCompletionValidationExpiresAt", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Missions_CustomerPaymentExpiresAt_Status",
                table: "Missions",
                columns: new[] { "CustomerPaymentExpiresAt", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Missions_CustomerCompletionValidationExpiresAt_Status",
                table: "Missions");

            migrationBuilder.DropIndex(
                name: "IX_Missions_CustomerPaymentExpiresAt_Status",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "CustomerCompletionValidationExpiresAt",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "CustomerPaymentExpiresAt",
                table: "Missions");
        }
    }
}
