using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServicePrestationIllustration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IllustrationUrl",
                table: "ServicePrestations",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IllustrationUrl",
                table: "ServicePrestations");
        }
    }
}
