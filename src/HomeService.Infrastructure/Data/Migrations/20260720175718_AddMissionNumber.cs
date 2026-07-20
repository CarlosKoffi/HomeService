using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMissionNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MissionNumber",
                table: "Missions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Missions"
                SET "MissionNumber" = upper(concat(
                    'MIS-',
                    to_char(coalesce("CreatedAt", now()), 'YYMMDD'),
                    '-',
                    substr(replace("Id"::text, '-', ''), 1, 8)
                ))
                WHERE "MissionNumber" IS NULL
                   OR trim("MissionNumber") = '';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "MissionNumber",
                table: "Missions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Missions_MissionNumber",
                table: "Missions",
                column: "MissionNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Missions_MissionNumber",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "MissionNumber",
                table: "Missions");
        }
    }
}
