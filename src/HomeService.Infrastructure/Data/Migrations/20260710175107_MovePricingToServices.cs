using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MovePricingToServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "ProviderServices");

            migrationBuilder.DropColumn(
                name: "HourlyRateAmount",
                table: "ProviderServices");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Services",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "XOF");

            migrationBuilder.AddColumn<int>(
                name: "NormalPriceAmount",
                table: "Services",
                type: "integer",
                nullable: false,
                defaultValue: 1500);

            migrationBuilder.AddColumn<int>(
                name: "PremiumPriceAmount",
                table: "Services",
                type: "integer",
                nullable: false,
                defaultValue: 2500);

            migrationBuilder.Sql("""
                UPDATE "Services"
                SET "Currency" = 'XOF',
                    "NormalPriceAmount" = CASE
                        WHEN "NormalizedName" = 'menage a domicile' THEN 3500
                        WHEN "NormalizedName" = 'nounou' THEN 4000
                        WHEN "NormalizedName" = 'jardinage' THEN 4500
                        ELSE "NormalPriceAmount"
                    END,
                    "PremiumPriceAmount" = CASE
                        WHEN "NormalizedName" = 'menage a domicile' THEN 5000
                        WHEN "NormalizedName" = 'nounou' THEN 6500
                        WHEN "NormalizedName" = 'jardinage' THEN 6500
                        ELSE "PremiumPriceAmount"
                    END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "NormalPriceAmount",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "PremiumPriceAmount",
                table: "Services");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "ProviderServices",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "XOF");

            migrationBuilder.AddColumn<int>(
                name: "HourlyRateAmount",
                table: "ProviderServices",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
