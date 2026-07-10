using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyPortalUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompanyApplicationStatusHistory_CompanyApplications_Company~",
                table: "CompanyApplicationStatusHistory");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CompanyApplicationStatusHistory",
                table: "CompanyApplicationStatusHistory");

            migrationBuilder.RenameTable(
                name: "CompanyApplicationStatusHistory",
                newName: "CompanyApplicationStatusHistories");

            migrationBuilder.RenameIndex(
                name: "IX_CompanyApplicationStatusHistory_CompanyApplicationId_Change~",
                table: "CompanyApplicationStatusHistories",
                newName: "IX_CompanyApplicationStatusHistories_CompanyApplicationId_Chan~");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CompanyApplicationStatusHistories",
                table: "CompanyApplicationStatusHistories",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "CompanyPortalUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IsOwner = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyPortalUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyPortalUsers_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyPortalUsers_CompanyId",
                table: "CompanyPortalUsers",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyPortalUsers_Email",
                table: "CompanyPortalUsers",
                column: "Email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyApplicationStatusHistories_CompanyApplications_Compa~",
                table: "CompanyApplicationStatusHistories",
                column: "CompanyApplicationId",
                principalTable: "CompanyApplications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompanyApplicationStatusHistories_CompanyApplications_Compa~",
                table: "CompanyApplicationStatusHistories");

            migrationBuilder.DropTable(
                name: "CompanyPortalUsers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CompanyApplicationStatusHistories",
                table: "CompanyApplicationStatusHistories");

            migrationBuilder.RenameTable(
                name: "CompanyApplicationStatusHistories",
                newName: "CompanyApplicationStatusHistory");

            migrationBuilder.RenameIndex(
                name: "IX_CompanyApplicationStatusHistories_CompanyApplicationId_Chan~",
                table: "CompanyApplicationStatusHistory",
                newName: "IX_CompanyApplicationStatusHistory_CompanyApplicationId_Change~");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CompanyApplicationStatusHistory",
                table: "CompanyApplicationStatusHistory",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyApplicationStatusHistory_CompanyApplications_Company~",
                table: "CompanyApplicationStatusHistory",
                column: "CompanyApplicationId",
                principalTable: "CompanyApplications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
