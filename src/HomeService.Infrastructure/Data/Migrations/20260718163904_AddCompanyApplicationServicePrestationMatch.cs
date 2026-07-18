using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyApplicationServicePrestationMatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MatchedServicePrestationId",
                table: "CompanyApplicationServices",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyApplicationServices_MatchedServicePrestationId",
                table: "CompanyApplicationServices",
                column: "MatchedServicePrestationId");

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyApplicationServices_ServicePrestations_MatchedServic~",
                table: "CompanyApplicationServices",
                column: "MatchedServicePrestationId",
                principalTable: "ServicePrestations",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompanyApplicationServices_ServicePrestations_MatchedServic~",
                table: "CompanyApplicationServices");

            migrationBuilder.DropIndex(
                name: "IX_CompanyApplicationServices_MatchedServicePrestationId",
                table: "CompanyApplicationServices");

            migrationBuilder.DropColumn(
                name: "MatchedServicePrestationId",
                table: "CompanyApplicationServices");
        }
    }
}
