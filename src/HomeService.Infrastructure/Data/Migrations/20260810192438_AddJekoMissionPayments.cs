using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJekoMissionPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MissionPaymentRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerPaymentMethodId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ExternalPaymentRequestId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ExternalTransactionId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ProviderCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CommercialAmount = table.Column<int>(type: "integer", nullable: false),
                    ProviderFeeAmount = table.Column<int>(type: "integer", nullable: false),
                    RequestedAmount = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    RedirectUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FailureMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissionPaymentRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MissionPaymentRequests_CustomerPaymentMethods_CustomerPayme~",
                        column: x => x.CustomerPaymentMethodId,
                        principalTable: "CustomerPaymentMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MissionPaymentRequests_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MissionPaymentRequests_Missions_MissionId",
                        column: x => x.MissionId,
                        principalTable: "Missions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MissionPaymentRequests_CustomerId",
                table: "MissionPaymentRequests",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_MissionPaymentRequests_CustomerPaymentMethodId",
                table: "MissionPaymentRequests",
                column: "CustomerPaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_MissionPaymentRequests_ExternalPaymentRequestId",
                table: "MissionPaymentRequests",
                column: "ExternalPaymentRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MissionPaymentRequests_ExternalTransactionId",
                table: "MissionPaymentRequests",
                column: "ExternalTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_MissionPaymentRequests_MissionId_Status_CreatedAt",
                table: "MissionPaymentRequests",
                columns: new[] { "MissionId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MissionPaymentRequests_OnePendingPerMission",
                table: "MissionPaymentRequests",
                column: "MissionId",
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_MissionPaymentRequests_Reference",
                table: "MissionPaymentRequests",
                column: "Reference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MissionPaymentRequests");
        }
    }
}
