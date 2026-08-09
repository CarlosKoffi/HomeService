using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Infrastructure;

public sealed class MigrationDiscoveryTests
{
    [Fact]
    public void Notification_delivery_rules_creation_is_registered_before_template_columns()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseNpgsql("Host=localhost;Database=migration-discovery;Username=test;Password=test")
            .Options;

        using var db = new HomeServiceDbContext(options);
        var migrations = db.Database.GetMigrations().ToArray();

        var createRulesIndex = Array.IndexOf(migrations, "20260720160000_AddNotificationDeliveryRules");
        var addTemplatesIndex = Array.IndexOf(migrations, "20260726004934_AddNotificationDeliveryTemplates");

        Assert.True(createRulesIndex >= 0, "The migration creating NotificationDeliveryRules must be discoverable.");
        Assert.True(addTemplatesIndex >= 0, "The migration adding notification templates must be discoverable.");
        Assert.True(createRulesIndex < addTemplatesIndex, "NotificationDeliveryRules must exist before template columns are added.");
    }
}
