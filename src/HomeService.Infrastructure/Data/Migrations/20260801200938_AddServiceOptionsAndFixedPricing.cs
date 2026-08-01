using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceOptionsAndFixedPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFixedPrice",
                table: "Services",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsFixedPrice",
                table: "ServicePrestations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ServiceOptionId",
                table: "Missions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ServiceOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServicePrestationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    PriceMinAmount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    PriceMaxAmount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsFixedPrice = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceOptions_ServicePrestations_ServicePrestationId",
                        column: x => x.ServicePrestationId,
                        principalTable: "ServicePrestations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Missions_ServiceOptionId_Status",
                table: "Missions",
                columns: new[] { "ServiceOptionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOptions_ServicePrestationId_IsActive_SortOrder",
                table: "ServiceOptions",
                columns: new[] { "ServicePrestationId", "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOptions_ServicePrestationId_NormalizedName",
                table: "ServiceOptions",
                columns: new[] { "ServicePrestationId", "NormalizedName" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Missions_ServiceOptions_ServiceOptionId",
                table: "Missions",
                column: "ServiceOptionId",
                principalTable: "ServiceOptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Missions_ServiceOptions_ServiceOptionId",
                table: "Missions");

            migrationBuilder.DropTable(
                name: "ServiceOptions");

            migrationBuilder.DropIndex(
                name: "IX_Missions_ServiceOptionId_Status",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "IsFixedPrice",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "IsFixedPrice",
                table: "ServicePrestations");

            migrationBuilder.DropColumn(
                name: "ServiceOptionId",
                table: "Missions");
        }
    }
}
