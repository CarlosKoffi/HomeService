using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations;

[DbContext(typeof(HomeServiceDbContext))]
[Migration("20260817120000_SeparatePersonalAndBusinessCustomerAccounts")]
public partial class SeparatePersonalAndBusinessCustomerAccounts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "AccountType",
            table: "Customers",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.Sql(
            """
            UPDATE "Customers"
            SET "AccountType" = 1
            WHERE "Id" IN (SELECT "CustomerProfileId" FROM "BusinessClientProfiles");
            """);

        migrationBuilder.DropIndex(
            name: "IX_Customers_PhoneNumber",
            table: "Customers");

        migrationBuilder.CreateIndex(
            name: "IX_Customers_PhoneNumber_AccountType",
            table: "Customers",
            columns: new[] { "PhoneNumber", "AccountType" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Customers_PhoneNumber_AccountType",
            table: "Customers");

        migrationBuilder.DropColumn(
            name: "AccountType",
            table: "Customers");

        migrationBuilder.CreateIndex(
            name: "IX_Customers_PhoneNumber",
            table: "Customers",
            column: "PhoneNumber");
    }
}
