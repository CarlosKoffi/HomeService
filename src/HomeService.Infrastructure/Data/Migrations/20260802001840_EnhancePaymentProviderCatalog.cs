using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnhancePaymentProviderCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "PaymentProviders",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "PaymentProviders");
        }
    }
}
