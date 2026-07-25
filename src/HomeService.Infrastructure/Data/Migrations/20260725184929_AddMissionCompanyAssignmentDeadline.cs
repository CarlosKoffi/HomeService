using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMissionCompanyAssignmentDeadline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompanyAssignmentExpiresAt",
                table: "Missions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Missions_CompanyAssignmentExpiresAt_Status",
                table: "Missions",
                columns: new[] { "CompanyAssignmentExpiresAt", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Missions_CompanyAssignmentExpiresAt_Status",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "CompanyAssignmentExpiresAt",
                table: "Missions");
        }
    }
}
