using HomeService.Application.Admin;
using HomeService.Application.Notifications;
using HomeService.Contracts.Notifications;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class NotificationTemplateServiceTests
{
    [Fact]
    public void DefaultCatalog_ShouldExposeTemplatesForEverySupportedChannelWithoutDuplicates()
    {
        var templateKeys = NotificationTemplateCatalog.Defaults
            .SelectMany(seed => seed.Channels.Select(channel => $"{seed.EventKey}|{channel}"))
            .ToList();

        Assert.Equal(templateKeys.Count, templateKeys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(NotificationTemplateCatalog.Defaults, seed =>
            seed.EventKey == "MissionAssignedToProvider"
            && seed.Channels.Contains(NotificationTemplateChannel.MobilePush)
            && seed.Channels.Contains(NotificationTemplateChannel.Email)
            && seed.Channels.Contains(NotificationTemplateChannel.WhatsApp));
        Assert.Contains(NotificationTemplateCatalog.Defaults, seed =>
            seed.EventKey == "CompanyApplicationApproved"
            && seed.Channels.Contains(NotificationTemplateChannel.Portal)
            && seed.Channels.Contains(NotificationTemplateChannel.Email)
            && seed.Channels.Contains(NotificationTemplateChannel.WhatsApp));
        Assert.Contains(NotificationTemplateCatalog.Defaults, seed =>
            seed.EventKey == "CompanyDocumentApproved"
            && seed.Channels.Contains(NotificationTemplateChannel.Portal));
        Assert.Contains(NotificationTemplateCatalog.Defaults, seed =>
            seed.EventKey == "MissionDisputeResolved"
            && seed.Channels.Contains(NotificationTemplateChannel.Portal));
    }

    [Fact]
    public async Task AdminTemplateService_ListAsync_ShouldSeedDefaultTemplates()
    {
        await using var db = CreateDbContext();

        var templates = await new AdminNotificationTemplateService(db).ListAsync(CancellationToken.None);

        Assert.True(templates.Count >= 90);
        Assert.Contains(templates, template => template.EventKey == "MissionCompleted" && template.Channel == "MobilePush");
        Assert.Contains(templates, template => template.EventKey == "MissionCompleted" && template.Channel == "Email");
        Assert.Contains(templates, template => template.EventKey == "MissionCompleted" && template.Channel == "WhatsApp");
        Assert.Contains(templates, template => template.EventKey == "CompanyDocumentApproved" && template.Channel == "Portal");
        Assert.Contains(templates, template => template.EventKey == "MissionDisputeResolved" && template.Channel == "Portal");
        Assert.True(await db.NotificationDeliveryRules.AnyAsync(rule => rule.EventKey == "MissionCompleted"));
        Assert.True(await db.NotificationDeliveryRules.AnyAsync(rule => rule.EventKey == "CompanyApplicationApproved"));
    }

    [Fact]
    public async Task AdminTemplateService_ListAsync_ShouldCreateDeliveryRulesMatchingEveryTemplateEvent()
    {
        await using var db = CreateDbContext();

        var templates = await new AdminNotificationTemplateService(db).ListAsync(CancellationToken.None);
        var rules = await db.NotificationDeliveryRules
            .AsNoTracking()
            .ToDictionaryAsync(rule => rule.EventKey, StringComparer.OrdinalIgnoreCase);

        foreach (var group in templates.GroupBy(template => template.EventKey, StringComparer.OrdinalIgnoreCase))
        {
            Assert.True(rules.TryGetValue(group.Key, out var rule), $"Missing delivery rule for {group.Key}.");

            foreach (var template in group)
            {
                Assert.Equal(template.Label, rule!.Label);
                Assert.Equal(template.Audience, rule.Audience);

                if (template.Channel == NotificationTemplateChannel.Portal.ToString())
                {
                    Assert.True(rule.PortalEnabled, $"Portal delivery is disabled for {template.EventKey}.");
                }

                if (template.Channel == NotificationTemplateChannel.MobilePush.ToString())
                {
                    Assert.True(rule.MobileAppEnabled, $"Mobile delivery is disabled for {template.EventKey}.");
                }

                if (template.Channel == NotificationTemplateChannel.Email.ToString())
                {
                    Assert.True(rule.EmailEnabled, $"Email delivery is disabled for {template.EventKey}.");
                }

                if (template.Channel == NotificationTemplateChannel.WhatsApp.ToString())
                {
                    Assert.True(rule.WhatsAppEnabled, $"WhatsApp delivery is disabled for {template.EventKey}.");
                }
            }
        }
    }

    [Fact]
    public async Task AdminTemplateService_CreateAsync_WhenDuplicateEventAndChannel_ReturnsConflict()
    {
        await using var db = CreateDbContext();
        var service = new AdminNotificationTemplateService(db);
        await service.ListAsync(CancellationToken.None);

        var result = await service.CreateAsync(
            new CreateNotificationTemplateRequest(
                "MissionCompleted",
                "Email",
                "Mission terminee",
                "Customer",
                "Sujet",
                "Message",
                NotificationTemplateCatalog.CommonVariables,
                true),
            CancellationToken.None);

        Assert.Equal(AdminNotificationTemplateStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task AdminTemplateService_CreateAsync_WhenRuleIsMissing_CreatesMatchingDeliveryRule()
    {
        await using var db = CreateDbContext();
        var service = new AdminNotificationTemplateService(db);

        var result = await service.CreateAsync(
            new CreateNotificationTemplateRequest(
                "CustomCompanyPortalEvent",
                "Portal",
                "Message portail entreprise",
                "Company",
                "Sujet portail",
                "Message portail",
                NotificationTemplateCatalog.CommonVariables,
                true),
            CancellationToken.None);

        Assert.Equal(AdminNotificationTemplateStatus.Ok, result.Status);
        var rule = await db.NotificationDeliveryRules.SingleAsync(rule => rule.EventKey == "CustomCompanyPortalEvent");
        Assert.True(rule.PortalEnabled);
        Assert.False(rule.MobileAppEnabled);
    }

    [Fact]
    public async Task RenderAsync_WhenTemplateExists_UsesEventAndChannelTemplate()
    {
        await using var db = CreateDbContext();
        db.NotificationTemplates.Add(new NotificationTemplate(
            "MissionAssignedToProvider",
            NotificationTemplateChannel.MobilePush,
            "Mission",
            "Provider",
            "Mission {NumeroMission}",
            "{NomPrestataire}, {Service} vous attend.",
            NotificationTemplateCatalog.CommonVariables));
        await db.SaveChangesAsync();

        var rendered = await new NotificationTemplateService(db).RenderAsync(
            "MissionAssignedToProvider",
            NotificationTemplateChannel.MobilePush,
            "Fallback subject",
            "Fallback body",
            NotificationTemplateRenderer.Variables(
                ("NumeroMission", "MS-123"),
                ("NomPrestataire", "Awa"),
                ("Service", "Menage")),
            CancellationToken.None);

        Assert.Equal("Mission MS-123", rendered.Subject);
        Assert.Equal("Awa, Menage vous attend.", rendered.Body);
    }

    [Fact]
    public async Task RenderAsync_WhenTemplateIsMissing_UsesFallback()
    {
        await using var db = CreateDbContext();

        var rendered = await new NotificationTemplateService(db).RenderAsync(
            "Unknown",
            NotificationTemplateChannel.Email,
            "Fallback {Service}",
            "Message {Service}",
            NotificationTemplateRenderer.Variables(("Service", "Jardinage")),
            CancellationToken.None);

        Assert.Equal("Fallback Jardinage", rendered.Subject);
        Assert.Equal("Message Jardinage", rendered.Body);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }
}
