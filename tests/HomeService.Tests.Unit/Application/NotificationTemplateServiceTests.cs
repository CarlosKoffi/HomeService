using HomeService.Application.Notifications;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class NotificationTemplateServiceTests
{
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
