using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyPortalEmployeeWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProviderServices_Providers_ProviderProfileId",
                table: "ProviderServices");

            migrationBuilder.DropIndex(
                name: "IX_ProviderServices_ProviderProfileId",
                table: "ProviderServices");

            migrationBuilder.DropIndex(
                name: "IX_Providers_CompanyId",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "ProviderProfileId",
                table: "ProviderServices");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Providers",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "Providers",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<decimal>(
                name: "CurrentLongitude",
                table: "Providers",
                type: "numeric(10,7)",
                precision: 10,
                scale: 7,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(9,6)",
                oldPrecision: 9,
                oldScale: 6,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "CurrentLatitude",
                table: "Providers",
                type: "numeric(10,7)",
                precision: 10,
                scale: 7,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(9,6)",
                oldPrecision: 9,
                oldScale: 6,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Providers",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfBirth",
                table: "Providers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmploymentType",
                table: "Providers",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "MissionLatitude",
                table: "Providers",
                type: "numeric(10,7)",
                precision: 10,
                scale: 7,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MissionLongitude",
                table: "Providers",
                type: "numeric(10,7)",
                precision: 10,
                scale: 7,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MissionRadiusKm",
                table: "Providers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "YearsOfExperience",
                table: "Providers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CompanyPortalSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyPortalUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyPortalSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyPortalSessions_CompanyPortalUsers_CompanyPortalUserId",
                        column: x => x.CompanyPortalUserId,
                        principalTable: "CompanyPortalUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProviderDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(700)", maxLength: 700, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderDocuments_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderServices_ServiceId",
                table: "ProviderServices",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Providers_CompanyId_Status",
                table: "Providers",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyPortalSessions_CompanyPortalUserId_ExpiresAt",
                table: "CompanyPortalSessions",
                columns: new[] { "CompanyPortalUserId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyPortalSessions_TokenHash",
                table: "CompanyPortalSessions",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProviderDocuments_ProviderId_DocumentType",
                table: "ProviderDocuments",
                columns: new[] { "ProviderId", "DocumentType" });

            migrationBuilder.AddForeignKey(
                name: "FK_ProviderServices_Providers_ProviderId",
                table: "ProviderServices",
                column: "ProviderId",
                principalTable: "Providers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProviderServices_Services_ServiceId",
                table: "ProviderServices",
                column: "ServiceId",
                principalTable: "Services",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProviderServices_Providers_ProviderId",
                table: "ProviderServices");

            migrationBuilder.DropForeignKey(
                name: "FK_ProviderServices_Services_ServiceId",
                table: "ProviderServices");

            migrationBuilder.DropTable(
                name: "CompanyPortalSessions");

            migrationBuilder.DropTable(
                name: "ProviderDocuments");

            migrationBuilder.DropIndex(
                name: "IX_ProviderServices_ServiceId",
                table: "ProviderServices");

            migrationBuilder.DropIndex(
                name: "IX_Providers_CompanyId_Status",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "EmploymentType",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "MissionLatitude",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "MissionLongitude",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "MissionRadiusKm",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "YearsOfExperience",
                table: "Providers");

            migrationBuilder.AddColumn<Guid>(
                name: "ProviderProfileId",
                table: "ProviderServices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Providers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "Providers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<decimal>(
                name: "CurrentLongitude",
                table: "Providers",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,7)",
                oldPrecision: 10,
                oldScale: 7,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "CurrentLatitude",
                table: "Providers",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,7)",
                oldPrecision: 10,
                oldScale: 7,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProviderServices_ProviderProfileId",
                table: "ProviderServices",
                column: "ProviderProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Providers_CompanyId",
                table: "Providers",
                column: "CompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProviderServices_Providers_ProviderProfileId",
                table: "ProviderServices",
                column: "ProviderProfileId",
                principalTable: "Providers",
                principalColumn: "Id");
        }
    }
}
