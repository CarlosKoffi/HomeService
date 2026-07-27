using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMissionAdditionalQuotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MissionAdditionalQuotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false),
                    RequestedPhotoStoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Amount = table.Column<int>(type: "integer", nullable: true),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    CompanyDescription = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: true),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RejectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PaymentReference = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissionAdditionalQuotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MissionAdditionalQuotes_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MissionAdditionalQuotes_Missions_MissionId",
                        column: x => x.MissionId,
                        principalTable: "Missions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MissionAdditionalQuotes_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MissionAdditionalQuotes_CompanyId_Status_RequestedAt",
                table: "MissionAdditionalQuotes",
                columns: new[] { "CompanyId", "Status", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MissionAdditionalQuotes_MissionId_Status",
                table: "MissionAdditionalQuotes",
                columns: new[] { "MissionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MissionAdditionalQuotes_ProviderId_Status_RequestedAt",
                table: "MissionAdditionalQuotes",
                columns: new[] { "ProviderId", "Status", "RequestedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MissionAdditionalQuotes");
        }
    }
}
