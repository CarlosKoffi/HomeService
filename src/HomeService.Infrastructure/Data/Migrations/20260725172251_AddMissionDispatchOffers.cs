using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMissionDispatchOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AcceptsUrgentMissions",
                table: "Companies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MissionDispatchPriority",
                table: "Companies",
                type: "integer",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.CreateTable(
                name: "MissionDispatchOffers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    ScoreDetails = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissionDispatchOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MissionDispatchOffers_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MissionDispatchOffers_Missions_MissionId",
                        column: x => x.MissionId,
                        principalTable: "Missions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Companies_Status_MissionDispatchPriority",
                table: "Companies",
                columns: new[] { "Status", "MissionDispatchPriority" });

            migrationBuilder.CreateIndex(
                name: "IX_MissionDispatchOffers_CompanyId_Status_ExpiresAt",
                table: "MissionDispatchOffers",
                columns: new[] { "CompanyId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MissionDispatchOffers_MissionId_CompanyId",
                table: "MissionDispatchOffers",
                columns: new[] { "MissionId", "CompanyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MissionDispatchOffers_MissionId_Status_Rank",
                table: "MissionDispatchOffers",
                columns: new[] { "MissionId", "Status", "Rank" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MissionDispatchOffers");

            migrationBuilder.DropIndex(
                name: "IX_Companies_Status_MissionDispatchPriority",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "AcceptsUrgentMissions",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "MissionDispatchPriority",
                table: "Companies");
        }
    }
}
