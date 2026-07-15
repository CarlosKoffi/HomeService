using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyProfileDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InterventionZones",
                table: "CompanyApplications",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalForm",
                table: "CompanyApplications",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrangeMoneyPaymentNumber",
                table: "CompanyApplications",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxIdentificationNumber",
                table: "CompanyApplications",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WavePaymentNumber",
                table: "CompanyApplications",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Companies",
                type: "character varying(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Companies",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InterventionZones",
                table: "Companies",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalForm",
                table: "Companies",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrangeMoneyPaymentNumber",
                table: "Companies",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlannedServices",
                table: "Companies",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationNumber",
                table: "Companies",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxIdentificationNumber",
                table: "Companies",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WavePaymentNumber",
                table: "Companies",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.Sql("""
                WITH latest_application AS (
                    SELECT DISTINCT ON ("CompanyId")
                        "CompanyId",
                        "CompanyName",
                        "RegistrationNumber",
                        "City",
                        "Address",
                        "PlannedServices",
                        "PhoneNumber",
                        "Email"
                    FROM "CompanyApplications"
                    WHERE "CompanyId" IS NOT NULL
                    ORDER BY "CompanyId", "CreatedAt" DESC
                )
                UPDATE "Companies" company
                SET
                    "Name" = COALESCE(NULLIF(company."Name", ''), latest_application."CompanyName"),
                    "RegistrationNumber" = COALESCE(company."RegistrationNumber", latest_application."RegistrationNumber"),
                    "City" = COALESCE(company."City", latest_application."City"),
                    "Address" = COALESCE(company."Address", latest_application."Address"),
                    "PlannedServices" = COALESCE(company."PlannedServices", latest_application."PlannedServices"),
                    "PhoneNumber" = COALESCE(NULLIF(company."PhoneNumber", ''), latest_application."PhoneNumber"),
                    "Email" = COALESCE(company."Email", latest_application."Email")
                FROM latest_application
                WHERE company."Id" = latest_application."CompanyId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InterventionZones",
                table: "CompanyApplications");

            migrationBuilder.DropColumn(
                name: "LegalForm",
                table: "CompanyApplications");

            migrationBuilder.DropColumn(
                name: "OrangeMoneyPaymentNumber",
                table: "CompanyApplications");

            migrationBuilder.DropColumn(
                name: "TaxIdentificationNumber",
                table: "CompanyApplications");

            migrationBuilder.DropColumn(
                name: "WavePaymentNumber",
                table: "CompanyApplications");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "InterventionZones",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "LegalForm",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "OrangeMoneyPaymentNumber",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "PlannedServices",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "RegistrationNumber",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "TaxIdentificationNumber",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "WavePaymentNumber",
                table: "Companies");
        }
    }
}
