using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyPortalActivities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompanyPortalActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Tone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: true),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyPortalActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyPortalActivities_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyPortalActivities_CompanyId_IsRead_OccurredAt",
                table: "CompanyPortalActivities",
                columns: new[] { "CompanyId", "IsRead", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyPortalActivities_CompanyId_OccurredAt",
                table: "CompanyPortalActivities",
                columns: new[] { "CompanyId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyPortalActivities");
        }
    }
}
