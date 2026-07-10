using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyApplicationValidationModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "CompanyApplications",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CompanyActivationTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevocationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyActivationTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyActivationTokens_CompanyApplications_CompanyApplicat~",
                        column: x => x.CompanyApplicationId,
                        principalTable: "CompanyApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompanyApplicationStatusHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    NewStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ChangedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyApplicationStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyApplicationStatusHistory_CompanyApplications_Company~",
                        column: x => x.CompanyApplicationId,
                        principalTable: "CompanyApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyApplications_CompanyId",
                table: "CompanyApplications",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyActivationTokens_CompanyApplicationId_ExpiresAt",
                table: "CompanyActivationTokens",
                columns: new[] { "CompanyApplicationId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyActivationTokens_TokenHash",
                table: "CompanyActivationTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyApplicationStatusHistory_CompanyApplicationId_Change~",
                table: "CompanyApplicationStatusHistory",
                columns: new[] { "CompanyApplicationId", "ChangedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyApplications_Companies_CompanyId",
                table: "CompanyApplications",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompanyApplications_Companies_CompanyId",
                table: "CompanyApplications");

            migrationBuilder.DropTable(
                name: "CompanyActivationTokens");

            migrationBuilder.DropTable(
                name: "CompanyApplicationStatusHistory");

            migrationBuilder.DropIndex(
                name: "IX_CompanyApplications_CompanyId",
                table: "CompanyApplications");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "CompanyApplications");
        }
    }
}
