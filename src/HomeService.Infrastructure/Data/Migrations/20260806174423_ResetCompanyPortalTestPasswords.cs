using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ResetCompanyPortalTestPasswords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM "CompanyPortalSessions";

                UPDATE "CompanyPortalUsers"
                SET "PasswordHash" = 'pbkdf2-sha256:210000:ujkrPXile6wimyqDsvluxw==:2YEeBWl5HwAdNmKrdqzbwiytYQeQJROIWqIgHBCJb34=',
                    "UpdatedAt" = now();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Password hashes and revoked sessions cannot be restored safely.
        }
    }
}
