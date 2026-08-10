using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyCommissionTiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompanyCommissionMissionSequence",
                table: "Missions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CompanyCommissionTierName",
                table: "Missions",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentCommissionRateBasisPoints",
                table: "Companies",
                type: "integer",
                nullable: false,
                defaultValue: 1500);

            migrationBuilder.AddColumn<int>(
                name: "CurrentCommissionTierMinimumMissionCount",
                table: "Companies",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "CurrentCommissionTierName",
                table: "Companies",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "Lancement");

            migrationBuilder.CreateTable(
                name: "CompanyCommissionTiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    MinimumMissionCount = table.Column<int>(type: "integer", nullable: false),
                    RateBasisPoints = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyCommissionTiers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyCommissionTiers_IsActive_SortOrder",
                table: "CompanyCommissionTiers",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyCommissionTiers_MinimumMissionCount",
                table: "CompanyCommissionTiers",
                column: "MinimumMissionCount",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyCommissionTiers");

            migrationBuilder.DropColumn(
                name: "CompanyCommissionMissionSequence",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "CompanyCommissionTierName",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "CurrentCommissionRateBasisPoints",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "CurrentCommissionTierMinimumMissionCount",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "CurrentCommissionTierName",
                table: "Companies");
        }
    }
}
