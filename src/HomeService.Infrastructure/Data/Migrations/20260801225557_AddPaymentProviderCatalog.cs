using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentProviderCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PaymentProviderId",
                table: "CustomerPaymentMethods",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentProviders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LogoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentProviders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentMethods_PaymentProviderId",
                table: "CustomerPaymentMethods",
                column: "PaymentProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentProviders_Code",
                table: "PaymentProviders",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentProviders_IsActive_SortOrder",
                table: "PaymentProviders",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerPaymentMethods_PaymentProviders_PaymentProviderId",
                table: "CustomerPaymentMethods",
                column: "PaymentProviderId",
                principalTable: "PaymentProviders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerPaymentMethods_PaymentProviders_PaymentProviderId",
                table: "CustomerPaymentMethods");

            migrationBuilder.DropTable(
                name: "PaymentProviders");

            migrationBuilder.DropIndex(
                name: "IX_CustomerPaymentMethods_PaymentProviderId",
                table: "CustomerPaymentMethods");

            migrationBuilder.DropColumn(
                name: "PaymentProviderId",
                table: "CustomerPaymentMethods");
        }
    }
}
