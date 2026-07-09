using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyApplicationRequestedServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "Services",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "CompanyApplicationServices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchedServiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    RawName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    MatchScore = table.Column<int>(type: "integer", nullable: true),
                    MatchStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ReviewNote = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyApplicationServices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyApplicationServices_CompanyApplications_CompanyAppli~",
                        column: x => x.CompanyApplicationId,
                        principalTable: "CompanyApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompanyApplicationServices_Services_MatchedServiceId",
                        column: x => x.MatchedServiceId,
                        principalTable: "Services",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Services_NormalizedName",
                table: "Services",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyApplicationServices_CompanyApplicationId_NormalizedN~",
                table: "CompanyApplicationServices",
                columns: new[] { "CompanyApplicationId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyApplicationServices_MatchedServiceId",
                table: "CompanyApplicationServices",
                column: "MatchedServiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyApplicationServices");

            migrationBuilder.DropIndex(
                name: "IX_Services_NormalizedName",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "Services");
        }
    }
}
