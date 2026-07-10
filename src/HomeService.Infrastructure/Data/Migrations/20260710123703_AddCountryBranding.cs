using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCountryBranding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CountryBrandings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryId = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PrimaryColor = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SecondaryColor = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AccentColor = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    HeroTitle = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    HeroSubtitle = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    HeroImageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MotifStyle = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CountryBrandings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CountryBrandings_Countries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CountryBrandings_CountryId",
                table: "CountryBrandings",
                column: "CountryId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CountryBrandings");
        }
    }
}
