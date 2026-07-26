using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventKey = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    Channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Label = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Audience = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SubjectTemplate = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    BodyTemplate = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AvailableVariables = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationTemplates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTemplates_Audience_Channel_IsActive",
                table: "NotificationTemplates",
                columns: new[] { "Audience", "Channel", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTemplates_EventKey_Channel",
                table: "NotificationTemplates",
                columns: new[] { "EventKey", "Channel" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationTemplates");
        }
    }
}
