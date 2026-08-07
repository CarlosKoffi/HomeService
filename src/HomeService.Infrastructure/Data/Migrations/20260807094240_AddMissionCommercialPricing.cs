using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMissionCommercialPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CommissionableAmount",
                table: "Missions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CustomerServiceFeeAmount",
                table: "Missions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CustomerServiceFeeRateBasisPoints",
                table: "Missions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CustomerTotalAmount",
                table: "Missions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsFirstCustomerCompanyOrder",
                table: "Missions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "PreferredCompanyId",
                table: "Missions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Missions_PreferredCompanyId_Status",
                table: "Missions",
                columns: new[] { "PreferredCompanyId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Missions_PreferredCompanyId_Status",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "CommissionableAmount",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "CustomerServiceFeeAmount",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "CustomerServiceFeeRateBasisPoints",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "CustomerTotalAmount",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "IsFirstCustomerCompanyOrder",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "PreferredCompanyId",
                table: "Missions");
        }
    }
}
