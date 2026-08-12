using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminMfaAndFinancialApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "LastAcceptedMfaTimeStep",
                table: "AdminUsers",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MfaEnabledAt",
                table: "AdminUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MfaSecretProtected",
                table: "AdminUsers",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PendingMfaExpiresAt",
                table: "AdminUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingMfaSecretProtected",
                table: "AdminUsers",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AdminFinancialApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminFinancialApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminFinancialApprovals_AdminUsers_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "AdminUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AdminMfaRecoveryCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminMfaRecoveryCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminMfaRecoveryCodes_AdminUsers_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "AdminUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminFinancialApprovals_AdminUserId_Operation_ResourceId_Pa~",
                table: "AdminFinancialApprovals",
                columns: new[] { "AdminUserId", "Operation", "ResourceId", "PayloadHash" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminFinancialApprovals_Operation_ResourceId_PayloadHash_Ex~",
                table: "AdminFinancialApprovals",
                columns: new[] { "Operation", "ResourceId", "PayloadHash", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminMfaRecoveryCodes_AdminUserId_CodeHash",
                table: "AdminMfaRecoveryCodes",
                columns: new[] { "AdminUserId", "CodeHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminFinancialApprovals");

            migrationBuilder.DropTable(
                name: "AdminMfaRecoveryCodes");

            migrationBuilder.DropColumn(
                name: "LastAcceptedMfaTimeStep",
                table: "AdminUsers");

            migrationBuilder.DropColumn(
                name: "MfaEnabledAt",
                table: "AdminUsers");

            migrationBuilder.DropColumn(
                name: "MfaSecretProtected",
                table: "AdminUsers");

            migrationBuilder.DropColumn(
                name: "PendingMfaExpiresAt",
                table: "AdminUsers");

            migrationBuilder.DropColumn(
                name: "PendingMfaSecretProtected",
                table: "AdminUsers");
        }
    }
}
