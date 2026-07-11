using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMissionConfirmationContactRelease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IconName",
                table: "Services",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "sparkles");

            migrationBuilder.AddColumn<int>(
                name: "MinimumPortfolioItems",
                table: "Services",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresAdminApprovalBeforeAssignment",
                table: "Services",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresBeforeAfterPhotos",
                table: "Services",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresCompletionPhoto",
                table: "Services",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresDiploma",
                table: "Services",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresPortfolio",
                table: "Services",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ArrivalToleranceMeters",
                table: "Missions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CancellationFeeAmount",
                table: "Missions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ContactDetailsReleasedAt",
                table: "Missions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CustomerConfirmedAt",
                table: "Missions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlatformCommissionAmount",
                table: "Missions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProviderAcceptedAt",
                table: "Missions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServiceAddress",
                table: "Missions",
                type: "character varying(360)",
                maxLength: 360,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ServiceLatitude",
                table: "Missions",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ServiceLongitude",
                table: "Missions",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TransportFeeAmount",
                table: "Missions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AuditLogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorDisplayName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: true),
                    Action = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    BeforeJson = table.Column<string>(type: "jsonb", nullable: true),
                    AfterJson = table.Column<string>(type: "jsonb", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MissionConversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissionConversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MissionConversations_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MissionConversations_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MissionConversations_Missions_MissionId",
                        column: x => x.MissionId,
                        principalTable: "Missions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MissionConversations_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProviderInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    InvitationLink = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderInvitations_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProviderInvitations_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProviderMissionAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RefusalReason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    RefusalComment = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    CompletionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CompletionPhotoPath = table.Column<string>(type: "character varying(640)", maxLength: 640, nullable: true),
                    OfferedLatitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    OfferedLongitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    OfferedAccuracyMeters = table.Column<int>(type: "integer", nullable: true),
                    AcceptedLatitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    AcceptedLongitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    AcceptedAccuracyMeters = table.Column<int>(type: "integer", nullable: true),
                    ArrivalLatitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    ArrivalLongitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    ArrivalAccuracyMeters = table.Column<int>(type: "integer", nullable: true),
                    ArrivalDistanceMeters = table.Column<int>(type: "integer", nullable: true),
                    ArrivalToleranceMeters = table.Column<int>(type: "integer", nullable: false),
                    ArrivalVerificationStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ArrivalVerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderMissionAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderMissionAssignments_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProviderMissionAssignments_Missions_MissionId",
                        column: x => x.MissionId,
                        principalTable: "Missions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProviderMissionAssignments_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProviderPortalSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderPortalSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderPortalSessions_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProviderServicePortfolioItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(640)", maxLength: 640, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderServicePortfolioItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderServicePortfolioItems_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProviderServicePortfolioItems_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MissionMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SenderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AttachmentPath = table.Column<string>(type: "character varying(640)", maxLength: 640, nullable: true),
                    AttachmentContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissionMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MissionMessages_MissionConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "MissionConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_ActorType_ActorId_OccurredAt",
                table: "AuditLogEntries",
                columns: new[] { "ActorType", "ActorId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_CorrelationId",
                table: "AuditLogEntries",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_EntityType_EntityId_OccurredAt",
                table: "AuditLogEntries",
                columns: new[] { "EntityType", "EntityId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_OccurredAt",
                table: "AuditLogEntries",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_MissionConversations_CompanyId",
                table: "MissionConversations",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_MissionConversations_CustomerId",
                table: "MissionConversations",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_MissionConversations_MissionId",
                table: "MissionConversations",
                column: "MissionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MissionConversations_ProviderId",
                table: "MissionConversations",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_MissionMessages_ConversationId_CreatedAt",
                table: "MissionMessages",
                columns: new[] { "ConversationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderInvitations_Code",
                table: "ProviderInvitations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProviderInvitations_CompanyId",
                table: "ProviderInvitations",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderInvitations_ProviderId_Status",
                table: "ProviderInvitations",
                columns: new[] { "ProviderId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderInvitations_TokenHash",
                table: "ProviderInvitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProviderMissionAssignments_CompanyId",
                table: "ProviderMissionAssignments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderMissionAssignments_MissionId_ProviderId",
                table: "ProviderMissionAssignments",
                columns: new[] { "MissionId", "ProviderId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderMissionAssignments_ProviderId_ArrivalVerificationSt~",
                table: "ProviderMissionAssignments",
                columns: new[] { "ProviderId", "ArrivalVerificationStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderMissionAssignments_ProviderId_Status",
                table: "ProviderMissionAssignments",
                columns: new[] { "ProviderId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderPortalSessions_ProviderId_ExpiresAt",
                table: "ProviderPortalSessions",
                columns: new[] { "ProviderId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderPortalSessions_TokenHash",
                table: "ProviderPortalSessions",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProviderServicePortfolioItems_ProviderId_ServiceId_DisplayO~",
                table: "ProviderServicePortfolioItems",
                columns: new[] { "ProviderId", "ServiceId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderServicePortfolioItems_ServiceId",
                table: "ProviderServicePortfolioItems",
                column: "ServiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogEntries");

            migrationBuilder.DropTable(
                name: "MissionMessages");

            migrationBuilder.DropTable(
                name: "ProviderInvitations");

            migrationBuilder.DropTable(
                name: "ProviderMissionAssignments");

            migrationBuilder.DropTable(
                name: "ProviderPortalSessions");

            migrationBuilder.DropTable(
                name: "ProviderServicePortfolioItems");

            migrationBuilder.DropTable(
                name: "MissionConversations");

            migrationBuilder.DropColumn(
                name: "IconName",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "MinimumPortfolioItems",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "RequiresAdminApprovalBeforeAssignment",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "RequiresBeforeAfterPhotos",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "RequiresCompletionPhoto",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "RequiresDiploma",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "RequiresPortfolio",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "ArrivalToleranceMeters",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "CancellationFeeAmount",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "ContactDetailsReleasedAt",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "CustomerConfirmedAt",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "PlatformCommissionAmount",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "ProviderAcceptedAt",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "ServiceAddress",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "ServiceLatitude",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "ServiceLongitude",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "TransportFeeAmount",
                table: "Missions");
        }
    }
}
