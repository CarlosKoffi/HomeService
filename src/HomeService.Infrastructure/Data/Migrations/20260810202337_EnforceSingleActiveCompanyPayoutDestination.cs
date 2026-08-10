using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSingleActiveCompanyPayoutDestination : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                WITH ranked_destinations AS (
                    SELECT "Id",
                           ROW_NUMBER() OVER (
                               PARTITION BY "CompanyId"
                               ORDER BY "IsDefault" DESC,
                                        COALESCE("UpdatedAt", "CreatedAt") DESC,
                                        "CreatedAt" DESC) AS position
                    FROM "CompanyPayoutDestinations"
                    WHERE "IsActive" = TRUE
                )
                UPDATE "CompanyPayoutDestinations" AS destination
                SET "IsActive" = FALSE,
                    "IsDefault" = FALSE,
                    "UpdatedAt" = CURRENT_TIMESTAMP
                FROM ranked_destinations AS ranked
                WHERE destination."Id" = ranked."Id"
                  AND ranked.position > 1;

                UPDATE "CompanyPayoutDestinations"
                SET "IsDefault" = TRUE,
                    "UpdatedAt" = CURRENT_TIMESTAMP
                WHERE "IsActive" = TRUE;
                """);

            migrationBuilder.CreateIndex(
                name: "UX_CompanyPayoutDestinations_CompanyId_Active",
                table: "CompanyPayoutDestinations",
                column: "CompanyId",
                unique: true,
                filter: "\"IsActive\" = TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_CompanyPayoutDestinations_CompanyId_Active",
                table: "CompanyPayoutDestinations");
        }
    }
}
