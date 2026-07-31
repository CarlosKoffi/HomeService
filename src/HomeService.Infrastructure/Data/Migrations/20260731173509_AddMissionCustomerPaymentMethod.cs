using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMissionCustomerPaymentMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomerPaymentMethodId",
                table: "Missions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Missions_CustomerPaymentMethodId",
                table: "Missions",
                column: "CustomerPaymentMethodId");

            migrationBuilder.AddForeignKey(
                name: "FK_Missions_CustomerPaymentMethods_CustomerPaymentMethodId",
                table: "Missions",
                column: "CustomerPaymentMethodId",
                principalTable: "CustomerPaymentMethods",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Missions_CustomerPaymentMethods_CustomerPaymentMethodId",
                table: "Missions");

            migrationBuilder.DropIndex(
                name: "IX_Missions_CustomerPaymentMethodId",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "CustomerPaymentMethodId",
                table: "Missions");
        }
    }
}
