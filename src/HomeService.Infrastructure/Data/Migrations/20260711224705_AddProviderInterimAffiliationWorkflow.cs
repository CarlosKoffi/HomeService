using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderInterimAffiliationWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Providers_Companies_CompanyId",
                table: "Providers");

            migrationBuilder.AlterColumn<Guid>(
                name: "CompanyId",
                table: "Providers",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "RegistrationSource",
                table: "Providers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "CompanyInvitation");

            migrationBuilder.CreateTable(
                name: "ProviderAffiliationRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Message = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: true),
                    ReviewNote = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: true),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderAffiliationRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderAffiliationRequests_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProviderAffiliationRequests_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProviderCandidateServices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExperienceLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    YearsOfExperience = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderCandidateServices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderCandidateServices_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProviderCandidateServices_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Providers_Status",
                table: "Providers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderAffiliationRequests_CompanyId_Status_RequestedAt",
                table: "ProviderAffiliationRequests",
                columns: new[] { "CompanyId", "Status", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderAffiliationRequests_ProviderId_CompanyId_Status",
                table: "ProviderAffiliationRequests",
                columns: new[] { "ProviderId", "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderCandidateServices_ProviderId_ServiceId",
                table: "ProviderCandidateServices",
                columns: new[] { "ProviderId", "ServiceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProviderCandidateServices_ServiceId_IsActive",
                table: "ProviderCandidateServices",
                columns: new[] { "ServiceId", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_Providers_Companies_CompanyId",
                table: "Providers",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Providers_Companies_CompanyId",
                table: "Providers");

            migrationBuilder.DropTable(
                name: "ProviderAffiliationRequests");

            migrationBuilder.DropTable(
                name: "ProviderCandidateServices");

            migrationBuilder.DropIndex(
                name: "IX_Providers_Status",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "RegistrationSource",
                table: "Providers");

            migrationBuilder.AlterColumn<Guid>(
                name: "CompanyId",
                table: "Providers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Providers_Companies_CompanyId",
                table: "Providers",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
