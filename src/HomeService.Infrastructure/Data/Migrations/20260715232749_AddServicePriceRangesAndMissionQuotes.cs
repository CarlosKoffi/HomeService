using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServicePriceRangesAndMissionQuotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PriceMaxAmount",
                table: "Services",
                type: "integer",
                nullable: false,
                defaultValue: 2500);

            migrationBuilder.AddColumn<int>(
                name: "PriceMinAmount",
                table: "Services",
                type: "integer",
                nullable: false,
                defaultValue: 1500);

            migrationBuilder.AddColumn<int>(
                name: "PriceMaxAmount",
                table: "ServicePrestations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PriceMinAmount",
                table: "ServicePrestations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE "Services"
                SET "PriceMinAmount" = GREATEST(0, "NormalPriceAmount"),
                    "PriceMaxAmount" = GREATEST(GREATEST(0, "NormalPriceAmount"), "PremiumPriceAmount");
                """);

            migrationBuilder.Sql("""
                UPDATE "ServicePrestations"
                SET "PriceMinAmount" = GREATEST(0, "NormalPriceAmount"),
                    "PriceMaxAmount" = GREATEST(GREATEST(0, "NormalPriceAmount"), "PremiumPriceAmount");
                """);

            migrationBuilder.AddColumn<string>(
                name: "CompanyQuoteJustification",
                table: "Missions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CompanyQuotedAmount",
                table: "Missions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompanyQuotedAt",
                table: "Missions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CustomerQuoteAcceptedAt",
                table: "Missions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PriceMaxAmount",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "PriceMinAmount",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "PriceMaxAmount",
                table: "ServicePrestations");

            migrationBuilder.DropColumn(
                name: "PriceMinAmount",
                table: "ServicePrestations");

            migrationBuilder.DropColumn(
                name: "CompanyQuoteJustification",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "CompanyQuotedAmount",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "CompanyQuotedAt",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "CustomerQuoteAcceptedAt",
                table: "Missions");
        }
    }
}
