using HomeService.Application.Admin;
using HomeService.Contracts.Notifications;
using HomeService.Domain.Entities;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class AdminNotificationDeliveryRuleServiceTests
{
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

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }
}
