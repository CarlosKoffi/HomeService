using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SyncNotificationDeliveryRulesModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The physical table is already created by the deployment SQL and
            // AddNotificationDeliveryRules migration. This migration synchronizes
            // the EF model snapshot so startup migrations no longer fail with
            // PendingModelChangesWarning.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
