using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMissionFinanceFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CompanyQuoteJustification",
                table: "Missions",
                type: "character varying(1200)",
                maxLength: 1200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignmentSource",
                table: "Missions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Company");

            migrationBuilder.AddColumn<int>(
                name: "CompanyPayoutAmount",
                table: "Missions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Missions",
                type: "character varying(1200)",
                maxLength: 1200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsInterimProviderSnapshot",
                table: "Missions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "KazaAssignmentCommissionRateBasisPoints",
                table: "Missions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PartsDescription",
                table: "Missions",
                type: "character varying(1200)",
                maxLength: 1200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PartsEstimateAmount",
                table: "Missions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlatformCommissionRateBasisPoints",
                table: "Missions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "QuoteStatus",
                table: "Missions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "NotRequired");

            migrationBuilder.AddColumn<bool>(
                name: "RequiresCompanyQuote",
                table: "Missions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ServicePrestationId",
                table: "Missions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CommissionRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Target = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ServicePrestationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignmentSource = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    RateBasisPoints = table.Column<int>(type: "integer", nullable: false),
                    FixedAmount = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EffectiveUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommissionRules_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CommissionRules_ServicePrestations_ServicePrestationId",
                        column: x => x.ServicePrestationId,
                        principalTable: "ServicePrestations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CommissionRules_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MissionAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttachmentType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(720)", maxLength: 720, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Caption = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissionAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MissionAttachments_Missions_MissionId",
                        column: x => x.MissionId,
                        principalTable: "Missions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MissionFinancialBreakdowns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissionFinancialBreakdowns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MissionFinancialBreakdowns_Missions_MissionId",
                        column: x => x.MissionId,
                        principalTable: "Missions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MissionPaymentMilestones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Trigger = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExternalPaymentReference = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissionPaymentMilestones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MissionPaymentMilestones_Missions_MissionId",
                        column: x => x.MissionId,
                        principalTable: "Missions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MissionStatusHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ToStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissionStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MissionStatusHistories_Missions_MissionId",
                        column: x => x.MissionId,
                        principalTable: "Missions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Missions_CompanyId_Status",
                table: "Missions",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Missions_QuoteStatus_PaymentStatus",
                table: "Missions",
                columns: new[] { "QuoteStatus", "PaymentStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_Missions_ServicePrestationId_Status",
                table: "Missions",
                columns: new[] { "ServicePrestationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CommissionRules_CompanyId",
                table: "CommissionRules",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionRules_ServiceId_ServicePrestationId_CompanyId_Ass~",
                table: "CommissionRules",
                columns: new[] { "ServiceId", "ServicePrestationId", "CompanyId", "AssignmentSource" });

            migrationBuilder.CreateIndex(
                name: "IX_CommissionRules_ServicePrestationId",
                table: "CommissionRules",
                column: "ServicePrestationId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionRules_Target_IsActive_EffectiveFrom",
                table: "CommissionRules",
                columns: new[] { "Target", "IsActive", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_MissionAttachments_MissionId_AttachmentType_IsDeleted",
                table: "MissionAttachments",
                columns: new[] { "MissionId", "AttachmentType", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_MissionFinancialBreakdowns_MissionId_LineType_SortOrder",
                table: "MissionFinancialBreakdowns",
                columns: new[] { "MissionId", "LineType", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MissionPaymentMilestones_MissionId_Trigger",
                table: "MissionPaymentMilestones",
                columns: new[] { "MissionId", "Trigger" });

            migrationBuilder.CreateIndex(
                name: "IX_MissionPaymentMilestones_Status_DueAt",
                table: "MissionPaymentMilestones",
                columns: new[] { "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MissionStatusHistories_MissionId_CreatedAt",
                table: "MissionStatusHistories",
                columns: new[] { "MissionId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Missions_ServicePrestations_ServicePrestationId",
                table: "Missions",
                column: "ServicePrestationId",
                principalTable: "ServicePrestations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Missions_ServicePrestations_ServicePrestationId",
                table: "Missions");

            migrationBuilder.DropTable(
                name: "CommissionRules");

            migrationBuilder.DropTable(
                name: "MissionAttachments");

            migrationBuilder.DropTable(
                name: "MissionFinancialBreakdowns");

            migrationBuilder.DropTable(
                name: "MissionPaymentMilestones");

            migrationBuilder.DropTable(
                name: "MissionStatusHistories");

            migrationBuilder.DropIndex(
                name: "IX_Missions_CompanyId_Status",
                table: "Missions");

            migrationBuilder.DropIndex(
                name: "IX_Missions_QuoteStatus_PaymentStatus",
                table: "Missions");

            migrationBuilder.DropIndex(
                name: "IX_Missions_ServicePrestationId_Status",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "AssignmentSource",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "CompanyPayoutAmount",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "IsInterimProviderSnapshot",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "KazaAssignmentCommissionRateBasisPoints",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "PartsDescription",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "PartsEstimateAmount",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "PlatformCommissionRateBasisPoints",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "QuoteStatus",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "RequiresCompanyQuote",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "ServicePrestationId",
                table: "Missions");

            migrationBuilder.AlterColumn<string>(
                name: "CompanyQuoteJustification",
                table: "Missions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1200)",
                oldMaxLength: 1200,
                oldNullable: true);
        }
    }
}
