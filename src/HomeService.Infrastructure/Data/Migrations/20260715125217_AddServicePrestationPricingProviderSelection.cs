using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServicePrestationPricingProviderSelection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "ServicePrestations",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "XOF");

            migrationBuilder.AddColumn<int>(
                name: "NormalPriceAmount",
                table: "ServicePrestations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PremiumPriceAmount",
                table: "ServicePrestations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ProviderServicePrestations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServicePrestationId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderServicePrestations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderServicePrestations_ProviderServices_ProviderService~",
                        column: x => x.ProviderServiceId,
                        principalTable: "ProviderServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProviderServicePrestations_ServicePrestations_ServicePresta~",
                        column: x => x.ServicePrestationId,
                        principalTable: "ServicePrestations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderServicePrestations_ProviderServiceId_ServicePrestat~",
                table: "ProviderServicePrestations",
                columns: new[] { "ProviderServiceId", "ServicePrestationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProviderServicePrestations_ServicePrestationId_IsActive",
                table: "ProviderServicePrestations",
                columns: new[] { "ServicePrestationId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProviderServicePrestations");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "ServicePrestations");

            migrationBuilder.DropColumn(
                name: "NormalPriceAmount",
                table: "ServicePrestations");

            migrationBuilder.DropColumn(
                name: "PremiumPriceAmount",
                table: "ServicePrestations");
        }
    }
}
