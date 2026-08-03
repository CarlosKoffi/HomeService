using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyMobileMoneyCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PresentationRating",
                table: "MissionReviews",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MoovMoneyPaymentNumber",
                table: "CompanyApplications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MtnMoneyPaymentNumber",
                table: "CompanyApplications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MoovMoneyPaymentNumber",
                table: "Companies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MtnMoneyPaymentNumber",
                table: "Companies",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PresentationRating",
                table: "MissionReviews");

            migrationBuilder.DropColumn(
                name: "MoovMoneyPaymentNumber",
                table: "CompanyApplications");

            migrationBuilder.DropColumn(
                name: "MtnMoneyPaymentNumber",
                table: "CompanyApplications");

            migrationBuilder.DropColumn(
                name: "MoovMoneyPaymentNumber",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "MtnMoneyPaymentNumber",
                table: "Companies");
        }
    }
}
