using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMissionDisputeRefundDecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "MissionDisputes",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "XOF");

            migrationBuilder.AddColumn<int>(
                name: "RefundAmount",
                table: "MissionDisputes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RefundPercentBasisPoints",
                table: "MissionDisputes",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "MissionDisputes");

            migrationBuilder.DropColumn(
                name: "RefundAmount",
                table: "MissionDisputes");

            migrationBuilder.DropColumn(
                name: "RefundPercentBasisPoints",
                table: "MissionDisputes");
        }
    }
}
