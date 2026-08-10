using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyWalletAndPayouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SettlementFrequency",
                table: "Companies",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Monthly");

            migrationBuilder.CreateTable(
                name: "CompanyPayoutDestinations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    BeneficiaryName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    ProtectedDetails = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    MaskedIdentifier = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ExternalContactId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyPayoutDestinations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyPayoutDestinations_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompanyWallets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PendingBalance = table.Column<int>(type: "integer", nullable: false),
                    AvailableBalance = table.Column<int>(type: "integer", nullable: false),
                    ReservedBalance = table.Column<int>(type: "integer", nullable: false),
                    WithdrawnBalance = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyWallets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyWallets_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompanyPayoutRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Frequency = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    GrossAmount = table.Column<int>(type: "integer", nullable: false),
                    FeeAmount = table.Column<int>(type: "integer", nullable: false),
                    NetAmount = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Reference = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PeriodStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PeriodEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExternalTransactionId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ProofReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProcessingAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyPayoutRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyPayoutRequests_CompanyPayoutDestinations_Destination~",
                        column: x => x.DestinationId,
                        principalTable: "CompanyPayoutDestinations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompanyWalletEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EligibleAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MissionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PayoutRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyWalletEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyWalletEntries_CompanyWallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "CompanyWallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyPayoutDestinations_CompanyId_IsActive",
                table: "CompanyPayoutDestinations",
                columns: new[] { "CompanyId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyPayoutDestinations_CompanyId_IsDefault",
                table: "CompanyPayoutDestinations",
                columns: new[] { "CompanyId", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyPayoutRequests_CompanyId_Status",
                table: "CompanyPayoutRequests",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyPayoutRequests_DestinationId",
                table: "CompanyPayoutRequests",
                column: "DestinationId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyPayoutRequests_ExternalTransactionId",
                table: "CompanyPayoutRequests",
                column: "ExternalTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyPayoutRequests_Reference",
                table: "CompanyPayoutRequests",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyWalletEntries_CompanyId_CreatedAt",
                table: "CompanyWalletEntries",
                columns: new[] { "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyWalletEntries_IdempotencyKey",
                table: "CompanyWalletEntries",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyWalletEntries_MissionId",
                table: "CompanyWalletEntries",
                column: "MissionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyWalletEntries_PayoutRequestId",
                table: "CompanyWalletEntries",
                column: "PayoutRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyWalletEntries_Type_EligibleAt",
                table: "CompanyWalletEntries",
                columns: new[] { "Type", "EligibleAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyWalletEntries_WalletId",
                table: "CompanyWalletEntries",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyWallets_CompanyId",
                table: "CompanyWallets",
                column: "CompanyId",
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO "CompanyWallets"
                    ("Id", "CompanyId", "PendingBalance", "AvailableBalance", "ReservedBalance", "WithdrawnBalance", "Currency", "Version", "CreatedAt", "UpdatedAt")
                SELECT
                    md5(c."Id"::text || ':company-wallet')::uuid,
                    c."Id",
                    0,
                    COALESCE(SUM(m."CompanyPayoutAmount") FILTER (
                        WHERE m."CompanyPayoutReleasedAt" IS NOT NULL AND m."CompanyPayoutAmount" > 0), 0)::integer,
                    0,
                    0,
                    'XOF',
                    0,
                    NOW(),
                    NULL
                FROM "Companies" c
                LEFT JOIN "Missions" m ON m."CompanyId" = c."Id"
                GROUP BY c."Id";

                INSERT INTO "CompanyWalletEntries"
                    ("Id", "CompanyId", "WalletId", "Type", "Amount", "Currency", "IdempotencyKey", "Description", "EligibleAt", "MissionId", "PayoutRequestId", "CreatedAt", "UpdatedAt")
                SELECT
                    md5(m."Id"::text || ':company-wallet-credit')::uuid,
                    m."CompanyId",
                    w."Id",
                    'FundsBecameAvailable',
                    m."CompanyPayoutAmount",
                    COALESCE(NULLIF(m."Currency", ''), 'XOF'),
                    'mission:' || replace(m."Id"::text, '-', '') || ':company-payout',
                    'Solde historique importe depuis la mission ' || m."MissionNumber" || '.',
                    NULL,
                    m."Id",
                    NULL,
                    COALESCE(m."CompanyPayoutReleasedAt", NOW()),
                    NULL
                FROM "Missions" m
                JOIN "CompanyWallets" w ON w."CompanyId" = m."CompanyId"
                WHERE m."CompanyId" IS NOT NULL
                  AND m."CompanyPayoutReleasedAt" IS NOT NULL
                  AND m."CompanyPayoutAmount" > 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyPayoutRequests");

            migrationBuilder.DropTable(
                name: "CompanyWalletEntries");

            migrationBuilder.DropTable(
                name: "CompanyPayoutDestinations");

            migrationBuilder.DropTable(
                name: "CompanyWallets");

            migrationBuilder.DropColumn(
                name: "SettlementFrequency",
                table: "Companies");
        }
    }
}
