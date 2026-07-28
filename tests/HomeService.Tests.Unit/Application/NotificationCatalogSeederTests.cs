using HomeService.Application.Notifications;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class NotificationCatalogSeederTests
{
    [Fact]
    public async Task EnsureDefaultsAsync_WhenCatalogIsEmpty_CreatesRulesAndTemplates()
    {
        await using var db = CreateDbContext();

        await new NotificationCatalogSeeder(db).EnsureDefaultsAsync(CancellationToken.None);

        var rules = await db.NotificationDeliveryRules.ToListAsync();
        var templates = await db.NotificationTemplates.ToListAsync();

        Assert.NotEmpty(rules);
        Assert.Contains(rules, rule => rule.EventKey == "MissionTechnicianArrived" && rule.MobileAppEnabled);
        Assert.Contains(rules, rule => rule.EventKey == "CompanyActivationLinkCreated" && rule.PortalEnabled);
        Assert.Contains(templates, template => template.EventKey == "MissionTechnicianArrived" && template.Channel == NotificationTemplateChannel.MobilePush);
        Assert.Contains(templates, template => template.EventKey == "CompanyActivationLinkCreated" && template.Channel == NotificationTemplateChannel.Portal);
    }

    [Fact]
    public async Task EnsureDefaultsAsync_WhenTemplateAlreadyExists_DoesNotOverwriteAdminText()
    {
        await using var db = CreateDbContext();
        db.NotificationTemplates.Add(new NotificationTemplate(
            "MissionCancelled",
            NotificationTemplateChannel.MobilePush,
            "Texte admin",
            "Customer",
            "Sujet admin",
            "Message admin",
            "{NumeroMission}"));
        await db.SaveChangesAsync();

        await new NotificationCatalogSeeder(db).EnsureDefaultsAsync(CancellationToken.None);

        var template = await db.NotificationTemplates.SingleAsync(item =>
            item.EventKey == "MissionCancelled"
            && item.Channel == NotificationTemplateChannel.MobilePush);
        Assert.Equal("Texte admin", template.Label);
        Assert.Equal("Sujet admin", template.SubjectTemplate);
        Assert.Equal("Message admin", template.BodyTemplate);
    }

    [Fact]
    public async Task EnsureDefaultsAsync_WhenCalledTwice_IsIdempotent()
    {
        await using var db = CreateDbContext();
        var seeder = new NotificationCatalogSeeder(db);

        await seeder.EnsureDefaultsAsync(CancellationToken.None);
        var ruleCount = await db.NotificationDeliveryRules.CountAsync();
        var templateCount = await db.NotificationTemplates.CountAsync();

        await seeder.EnsureDefaultsAsync(CancellationToken.None);

        Assert.Equal(ruleCount, await db.NotificationDeliveryRules.CountAsync());
        Assert.Equal(templateCount, await db.NotificationTemplates.CountAsync());
    }

    [Fact]
    public async Task EnsureDefaultsAsync_CreatesRuleAndEveryChannelTemplateForEachCatalogEvent()
    {
        await using var db = CreateDbContext();

        await new NotificationCatalogSeeder(db).EnsureDefaultsAsync(CancellationToken.None);

        var rules = await db.NotificationDeliveryRules
            .AsNoTracking()
            .ToListAsync();
        var templates = await db.NotificationTemplates
            .AsNoTracking()
            .ToListAsync();

        foreach (var seed in NotificationTemplateCatalog.Defaults)
        {
            Assert.Contains(rules, rule => rule.EventKey == seed.EventKey);

            foreach (var channel in seed.Channels)
            {
                Assert.Contains(templates, template =>
                    template.EventKey == seed.EventKey
                    && template.Channel == channel
                    && template.Audience == seed.Audience
                    && template.IsActive);
            }
        }
    }

    [Fact]
    public async Task EnsureDefaultsAsync_CreatesDeliveryRuleChannelsMatchingTemplateChannels()
    {
        await using var db = CreateDbContext();

        await new NotificationCatalogSeeder(db).EnsureDefaultsAsync(CancellationToken.None);

        var rules = await db.NotificationDeliveryRules
            .AsNoTracking()
            .ToDictionaryAsync(rule => rule.EventKey);

        foreach (var seed in NotificationTemplateCatalog.Defaults)
        {
            var rule = rules[seed.EventKey];
            Assert.Equal(seed.Channels.Contains(NotificationTemplateChannel.Portal), rule.PortalEnabled);
            Assert.Equal(seed.Channels.Contains(NotificationTemplateChannel.MobilePush), rule.MobileAppEnabled);
            Assert.Equal(seed.Channels.Contains(NotificationTemplateChannel.Email), rule.EmailEnabled);
            Assert.Equal(seed.Channels.Contains(NotificationTemplateChannel.WhatsApp), rule.WhatsAppEnabled);
        }
    }

    [Fact]
    public async Task EnsureDefaultsAsync_LinksEveryTemplateToDeliveryRule()
    {
        await using var db = CreateDbContext();

        await new NotificationCatalogSeeder(db).EnsureDefaultsAsync(CancellationToken.None);

        var templates = await db.NotificationTemplates
            .Include(template => template.DeliveryRule)
            .AsNoTracking()
            .ToListAsync();

        Assert.All(templates, template =>
        {
            Assert.NotNull(template.DeliveryRule);
            Assert.Equal(template.EventKey, template.DeliveryRule!.EventKey);
        });
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }
}
