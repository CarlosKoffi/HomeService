using HomeService.Application.Admin;
using HomeService.Application.Auditing;
using HomeService.Contracts.Notifications;
using HomeService.Domain.Entities;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HomeService.Tests.Unit.Application;

public sealed class AdminNotificationDeliveryRuleServiceTests
{
    [Fact]
    public async Task ListAsync_WhenCatalogIsEmpty_SeedsDefaultDeliveryRules()
    {
        await using var db = CreateDbContext();

        var rules = await new AdminNotificationDeliveryRuleService(db).ListAsync(CancellationToken.None);

        Assert.NotEmpty(rules);
        Assert.Contains(rules, rule =>
            rule.EventKey == "CompanyApplicationApproved"
            && rule.Audience == "Company"
            && rule.PortalEnabled
            && rule.EmailEnabled
            && rule.WhatsAppEnabled);
        Assert.Contains(rules, rule =>
            rule.EventKey == "MissionTechnicianArrived"
            && rule.Audience == "Customer"
            && rule.MobileAppEnabled
            && rule.EmailEnabled
            && rule.WhatsAppEnabled);
        Assert.Equal(rules.Count, await db.NotificationDeliveryRules.CountAsync());
    }

    [Fact]
    public async Task UpdateAsync_WhenAudienceChanges_NormalizesAutomaticChannels()
    {
        await using var db = CreateDbContext();
        var rule = new NotificationDeliveryRule(
            "MissionAssignedToProvider",
            "Mission affectee",
            "Company",
            portalEnabled: true,
            mobileAppEnabled: false,
            emailEnabled: true,
            whatsAppEnabled: true,
            subjectTemplate: "Sujet",
            bodyTemplate: "Message");
        db.NotificationDeliveryRules.Add(rule);
        await db.SaveChangesAsync();

        var result = await new AdminNotificationDeliveryRuleService(db).UpdateAsync(
            rule.Id,
            new UpdateNotificationDeliveryRuleRequest(
                "Mission affectee",
                "Provider",
                PortalEnabled: true,
                MobileAppEnabled: false,
                EmailEnabled: false,
                WhatsAppEnabled: true,
                SubjectTemplate: "Sujet",
                BodyTemplate: "Message"),
            CancellationToken.None);

        Assert.Equal(AdminNotificationDeliveryRuleStatus.Ok, result.Status);
        Assert.False(result.Response!.PortalEnabled);
        Assert.True(result.Response.MobileAppEnabled);
        Assert.False(result.Response.EmailEnabled);
        Assert.True(result.Response.WhatsAppEnabled);
    }

    [Fact]
    public async Task UpdateAsync_WhenAudienceIsUnknown_ReturnsValidationFailed()
    {
        await using var db = CreateDbContext();
        var rule = new NotificationDeliveryRule(
            "ManualExternalEvent",
            "Message manuel",
            "Company",
            portalEnabled: true,
            mobileAppEnabled: false,
            emailEnabled: true,
            whatsAppEnabled: false,
            subjectTemplate: "Sujet",
            bodyTemplate: "Message");
        db.NotificationDeliveryRules.Add(rule);
        await db.SaveChangesAsync();

        var result = await new AdminNotificationDeliveryRuleService(db).UpdateAsync(
            rule.Id,
            new UpdateNotificationDeliveryRuleRequest(
                "Message manuel",
                "Unknown",
                PortalEnabled: false,
                MobileAppEnabled: false,
                EmailEnabled: false,
                WhatsAppEnabled: false,
                SubjectTemplate: "Sujet",
                BodyTemplate: "Message"),
            CancellationToken.None);

        Assert.Equal(AdminNotificationDeliveryRuleStatus.ValidationFailed, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_ShouldPersistDeliveryRuleChanges()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var databaseRoot = new InMemoryDatabaseRoot();
        Guid ruleId;

        await using (var db = CreateDbContext(databaseName, databaseRoot))
        {
            var rule = new NotificationDeliveryRule(
                "MissionQuoteSentToCustomer",
                "Ancien libelle",
                "Customer",
                portalEnabled: false,
                mobileAppEnabled: true,
                emailEnabled: true,
                whatsAppEnabled: false,
                subjectTemplate: "Ancien sujet",
                bodyTemplate: "Ancien message");
            db.NotificationDeliveryRules.Add(rule);
            await db.SaveChangesAsync();
            ruleId = rule.Id;

            var result = await new AdminNotificationDeliveryRuleService(db).UpdateAsync(
                ruleId,
                new UpdateNotificationDeliveryRuleRequest(
                    "Devis envoye au client",
                    "Customer",
                    PortalEnabled: false,
                    MobileAppEnabled: true,
                    EmailEnabled: false,
                    WhatsAppEnabled: true,
                    SubjectTemplate: "Nouveau sujet",
                    BodyTemplate: "Nouveau message"),
                CancellationToken.None);

            Assert.Equal(AdminNotificationDeliveryRuleStatus.Ok, result.Status);
        }

        await using (var db = CreateDbContext(databaseName, databaseRoot))
        {
            var rule = await db.NotificationDeliveryRules.SingleAsync(item => item.Id == ruleId);
            Assert.Equal("Devis envoye au client", rule.Label);
            Assert.False(rule.PortalEnabled);
            Assert.True(rule.MobileAppEnabled);
            Assert.False(rule.EmailEnabled);
            Assert.True(rule.WhatsAppEnabled);
            Assert.Equal("Nouveau sujet", rule.SubjectTemplate);
            Assert.Equal("Nouveau message", rule.BodyTemplate);
        }
    }

    [Fact]
    public async Task UpdateAsync_WhenAuditActorIsProvided_CreatesAuditLog()
    {
        await using var db = CreateDbContext();
        var rule = new NotificationDeliveryRule(
            "MissionAssignedToProvider",
            "Mission affectee",
            "Provider",
            portalEnabled: false,
            mobileAppEnabled: true,
            emailEnabled: true,
            whatsAppEnabled: false,
            subjectTemplate: "Sujet",
            bodyTemplate: "Message");
        db.NotificationDeliveryRules.Add(rule);
        await db.SaveChangesAsync();

        var result = await new AdminNotificationDeliveryRuleService(db).UpdateAsync(
            rule.Id,
            new UpdateNotificationDeliveryRuleRequest(
                "Mission prestataire",
                "Provider",
                PortalEnabled: false,
                MobileAppEnabled: true,
                EmailEnabled: false,
                WhatsAppEnabled: true,
                SubjectTemplate: "Sujet",
                BodyTemplate: "Message"),
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "tests", "rule-audit"),
            CancellationToken.None);

        Assert.Equal(AdminNotificationDeliveryRuleStatus.Ok, result.Status);
        var log = Assert.Single(db.AuditLogEntries);
        Assert.Equal("AdminNotificationDeliveryRuleUpdated", log.Action);
        Assert.Equal(nameof(NotificationDeliveryRule), log.EntityType);
        Assert.Equal(rule.Id, log.EntityId);
        Assert.Equal("rule-audit", log.CorrelationId);
        Assert.Contains("Mission affectee", log.BeforeJson);
        Assert.Contains("Mission prestataire", log.AfterJson);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }

    private static HomeServiceDbContext CreateDbContext(string databaseName, InMemoryDatabaseRoot databaseRoot)
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(databaseName, databaseRoot)
            .Options;

        return new HomeServiceDbContext(options);
    }
}
