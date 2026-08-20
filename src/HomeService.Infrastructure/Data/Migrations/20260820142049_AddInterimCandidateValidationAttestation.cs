using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInterimCandidateValidationAttestation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CandidateMetAndTestedByCompany",
                table: "ProviderAffiliationRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompanyValidationAttestedAt",
                table: "ProviderAffiliationRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CompetencyValidatedByCompany",
                table: "ProviderAffiliationRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PunctualityValidatedByCompany",
                table: "ProviderAffiliationRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SeriousnessValidatedByCompany",
                table: "ProviderAffiliationRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CandidateMetAndTestedByCompany",
                table: "ProviderAffiliationRequests");

            migrationBuilder.DropColumn(
                name: "CompanyValidationAttestedAt",
                table: "ProviderAffiliationRequests");

            migrationBuilder.DropColumn(
                name: "CompetencyValidatedByCompany",
                table: "ProviderAffiliationRequests");

            migrationBuilder.DropColumn(
                name: "PunctualityValidatedByCompany",
                table: "ProviderAffiliationRequests");

            migrationBuilder.DropColumn(
                name: "SeriousnessValidatedByCompany",
                table: "ProviderAffiliationRequests");
        }
    }
}
