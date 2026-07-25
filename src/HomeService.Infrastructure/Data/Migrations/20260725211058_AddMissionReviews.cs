using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMissionReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompanyPayoutReleasedAt",
                table: "Missions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CustomerCompletionValidatedAt",
                table: "Missions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MissionReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    QualityRating = table.Column<int>(type: "integer", nullable: false),
                    PunctualityRating = table.Column<int>(type: "integer", nullable: false),
                    PolitenessRating = table.Column<int>(type: "integer", nullable: false),
                    CleanlinessRating = table.Column<int>(type: "integer", nullable: false),
                    OverallRating = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissionReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MissionReviews_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MissionReviews_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MissionReviews_Missions_MissionId",
                        column: x => x.MissionId,
                        principalTable: "Missions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MissionReviews_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Missions_CustomerCompletionValidatedAt_CompanyPayoutRelease~",
                table: "Missions",
                columns: new[] { "CustomerCompletionValidatedAt", "CompanyPayoutReleasedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MissionReviews_CompanyId_SubmittedAt",
                table: "MissionReviews",
                columns: new[] { "CompanyId", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MissionReviews_CustomerId",
                table: "MissionReviews",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_MissionReviews_MissionId",
                table: "MissionReviews",
                column: "MissionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MissionReviews_ProviderId_SubmittedAt",
                table: "MissionReviews",
                columns: new[] { "ProviderId", "SubmittedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MissionReviews");

            migrationBuilder.DropIndex(
                name: "IX_Missions_CustomerCompletionValidatedAt_CompanyPayoutRelease~",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "CompanyPayoutReleasedAt",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "CustomerCompletionValidatedAt",
                table: "Missions");
        }
    }
}
