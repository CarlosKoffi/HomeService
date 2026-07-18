using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyPortalNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompanyPortalNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyApplicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyApplicationDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Message = table.Column<string>(type: "character varying(700)", maxLength: 700, nullable: false),
                    Tone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActionUrl = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyPortalNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyPortalNotifications_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompanyPortalNotifications_CompanyApplicationDocuments_Comp~",
                        column: x => x.CompanyApplicationDocumentId,
                        principalTable: "CompanyApplicationDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CompanyPortalNotifications_CompanyApplications_CompanyAppli~",
                        column: x => x.CompanyApplicationId,
                        principalTable: "CompanyApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyPortalNotifications_CompanyApplicationDocumentId_Occ~",
                table: "CompanyPortalNotifications",
                columns: new[] { "CompanyApplicationDocumentId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyPortalNotifications_CompanyApplicationId_OccurredAt",
                table: "CompanyPortalNotifications",
                columns: new[] { "CompanyApplicationId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyPortalNotifications_CompanyId_IsRead_OccurredAt",
                table: "CompanyPortalNotifications",
                columns: new[] { "CompanyId", "IsRead", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyPortalNotifications");
        }
    }
}
