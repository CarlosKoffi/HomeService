using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQualityControlSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompanyQualitySummaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServicePrestationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    CompletedMissionCount = table.Column<int>(type: "integer", nullable: false),
                    EligibleProviderCount = table.Column<int>(type: "integer", nullable: false),
                    AverageRating = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    AuditPassRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    CalculatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyQualitySummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyQualitySummaries_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompanyQualitySummaries_ServicePrestations_ServicePrestatio~",
                        column: x => x.ServicePrestationId,
                        principalTable: "ServicePrestations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompanyQualitySummaries_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MissionQualityAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServicePrestationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SamplingReason = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    ReviewedByAdminUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    ReviewNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissionQualityAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MissionQualityAudits_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MissionQualityAudits_Missions_MissionId",
                        column: x => x.MissionId,
                        principalTable: "Missions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MissionQualityAudits_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MissionQualityAudits_ServicePrestations_ServicePrestationId",
                        column: x => x.ServicePrestationId,
                        principalTable: "ServicePrestations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MissionQualityAudits_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProviderPrestationQualifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServicePrestationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TheoryScore = table.Column<int>(type: "integer", nullable: true),
                    PracticalScore = table.Column<int>(type: "integer", nullable: true),
                    ReviewNote = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: true),
                    ReviewedByAdminUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderPrestationQualifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderPrestationQualifications_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProviderPrestationQualifications_ServicePrestations_Service~",
                        column: x => x.ServicePrestationId,
                        principalTable: "ServicePrestations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProviderQualitySummaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServicePrestationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    CompletedMissionCount = table.Column<int>(type: "integer", nullable: false),
                    AuditedMissionCount = table.Column<int>(type: "integer", nullable: false),
                    PassedAuditCount = table.Column<int>(type: "integer", nullable: false),
                    ConfirmedIncidentCount = table.Column<int>(type: "integer", nullable: false),
                    AverageRating = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    PunctualityRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    CalculatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderQualitySummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderQualitySummaries_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProviderQualitySummaries_ServicePrestations_ServicePrestati~",
                        column: x => x.ServicePrestationId,
                        principalTable: "ServicePrestations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProviderQualitySummaries_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QualityChecklistTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServicePrestationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityChecklistTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QualityChecklistTemplates_ServicePrestations_ServicePrestat~",
                        column: x => x.ServicePrestationId,
                        principalTable: "ServicePrestations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QualityChecklistTemplates_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MissionQualityControls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateVersion = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissionQualityControls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MissionQualityControls_Missions_MissionId",
                        column: x => x.MissionId,
                        principalTable: "Missions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MissionQualityControls_QualityChecklistTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "QualityChecklistTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QualityChecklistItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceOptionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Label = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Guidance = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    ResponseType = table.Column<int>(type: "integer", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresEvidenceOnIssue = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityChecklistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QualityChecklistItems_QualityChecklistTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "QualityChecklistTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QualityChecklistItems_ServiceOptions_ServiceOptionId",
                        column: x => x.ServiceOptionId,
                        principalTable: "ServiceOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MissionQualityItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ControlId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Label = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Guidance = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    ResponseType = table.Column<int>(type: "integer", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresEvidenceOnIssue = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    BooleanValue = table.Column<bool>(type: "boolean", nullable: true),
                    NumberValue = table.Column<decimal>(type: "numeric", nullable: true),
                    TextValue = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: true),
                    EvidenceAttachmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissionQualityItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MissionQualityItems_MissionAttachments_EvidenceAttachmentId",
                        column: x => x.EvidenceAttachmentId,
                        principalTable: "MissionAttachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MissionQualityItems_MissionQualityControls_ControlId",
                        column: x => x.ControlId,
                        principalTable: "MissionQualityControls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyQualitySummaries_CompanyId_ServiceId_ServicePrestati~",
                table: "CompanyQualitySummaries",
                columns: new[] { "CompanyId", "ServiceId", "ServicePrestationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyQualitySummaries_ServiceId_ServicePrestationId_Score",
                table: "CompanyQualitySummaries",
                columns: new[] { "ServiceId", "ServicePrestationId", "Score" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyQualitySummaries_ServicePrestationId",
                table: "CompanyQualitySummaries",
                column: "ServicePrestationId");

            migrationBuilder.CreateIndex(
                name: "IX_MissionQualityAudits_CompanyId",
                table: "MissionQualityAudits",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_MissionQualityAudits_MissionId",
                table: "MissionQualityAudits",
                column: "MissionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MissionQualityAudits_ProviderId_CreatedAt",
                table: "MissionQualityAudits",
                columns: new[] { "ProviderId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MissionQualityAudits_ServiceId",
                table: "MissionQualityAudits",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_MissionQualityAudits_ServicePrestationId",
                table: "MissionQualityAudits",
                column: "ServicePrestationId");

            migrationBuilder.CreateIndex(
                name: "IX_MissionQualityAudits_Status_CreatedAt",
                table: "MissionQualityAudits",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MissionQualityControls_MissionId",
                table: "MissionQualityControls",
                column: "MissionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MissionQualityControls_Status",
                table: "MissionQualityControls",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MissionQualityControls_TemplateId",
                table: "MissionQualityControls",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_MissionQualityItems_ControlId_Code",
                table: "MissionQualityItems",
                columns: new[] { "ControlId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MissionQualityItems_ControlId_Stage_SortOrder",
                table: "MissionQualityItems",
                columns: new[] { "ControlId", "Stage", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MissionQualityItems_EvidenceAttachmentId",
                table: "MissionQualityItems",
                column: "EvidenceAttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderPrestationQualifications_ProviderId_ServicePrestati~",
                table: "ProviderPrestationQualifications",
                columns: new[] { "ProviderId", "ServicePrestationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProviderPrestationQualifications_ServicePrestationId_Status",
                table: "ProviderPrestationQualifications",
                columns: new[] { "ServicePrestationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderQualitySummaries_ProviderId_ServiceId_ServicePresta~",
                table: "ProviderQualitySummaries",
                columns: new[] { "ProviderId", "ServiceId", "ServicePrestationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProviderQualitySummaries_ServiceId_ServicePrestationId_Score",
                table: "ProviderQualitySummaries",
                columns: new[] { "ServiceId", "ServicePrestationId", "Score" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderQualitySummaries_ServicePrestationId",
                table: "ProviderQualitySummaries",
                column: "ServicePrestationId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityChecklistItems_ServiceOptionId",
                table: "QualityChecklistItems",
                column: "ServiceOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityChecklistItems_TemplateId_Code",
                table: "QualityChecklistItems",
                columns: new[] { "TemplateId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QualityChecklistItems_TemplateId_Stage_SortOrder",
                table: "QualityChecklistItems",
                columns: new[] { "TemplateId", "Stage", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_QualityChecklistTemplates_IsActive_ServiceId",
                table: "QualityChecklistTemplates",
                columns: new[] { "IsActive", "ServiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_QualityChecklistTemplates_ServiceId_ServicePrestationId_Ver~",
                table: "QualityChecklistTemplates",
                columns: new[] { "ServiceId", "ServicePrestationId", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_QualityChecklistTemplates_ServicePrestationId",
                table: "QualityChecklistTemplates",
                column: "ServicePrestationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyQualitySummaries");

            migrationBuilder.DropTable(
                name: "MissionQualityAudits");

            migrationBuilder.DropTable(
                name: "MissionQualityItems");

            migrationBuilder.DropTable(
                name: "ProviderPrestationQualifications");

            migrationBuilder.DropTable(
                name: "ProviderQualitySummaries");

            migrationBuilder.DropTable(
                name: "QualityChecklistItems");

            migrationBuilder.DropTable(
                name: "MissionQualityControls");

            migrationBuilder.DropTable(
                name: "QualityChecklistTemplates");
        }
    }
}
