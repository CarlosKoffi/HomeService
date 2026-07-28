using HomeService.Application.Admin;
using HomeService.Application.Auditing;
using HomeService.Contracts.Notifications;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HomeService.Tests.Unit.Application;

public sealed class AdminNotificationTemplateServiceTests
{
    [Fact]
    public async Task ListAsync_WhenCatalogIsEmpty_SeedsDefaultTemplates()
    {
        await using var db = CreateDbContext();

        var templates = await new AdminNotificationTemplateService(db).ListAsync(CancellationToken.None);

        Assert.NotEmpty(templates);
        Assert.Contains(templates, template =>
            template.EventKey == "CompanyApplicationApproved"
            && template.Channel == nameof(NotificationTemplateChannel.Portal)
            && template.Audience == "Company");
        Assert.Contains(templates, template =>
            template.EventKey == "MissionTechnicianArrived"
            && template.Channel == nameof(NotificationTemplateChannel.MobilePush)
            && template.Audience == "Customer");
    }

    [Fact]
    public async Task CreateAsync_WhenRuleDoesNotExist_CreatesTemplateAndDeliveryRule()
    {
        await using var db = CreateDbContext();

        var result = await new AdminNotificationTemplateService(db).CreateAsync(
            new CreateNotificationTemplateRequest(
                "ManualCompanyNotice",
                nameof(NotificationTemplateChannel.Portal),
                "Message portail entreprise",
                "Company",
                "Information dossier",
                "Bonjour {NomEntreprise}, votre dossier avance.",
                "{NomEntreprise}",
                true),
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "tests", "template-create"),
            CancellationToken.None);

        Assert.Equal(AdminNotificationTemplateStatus.Ok, result.Status);
        Assert.Equal("ManualCompanyNotice", result.Response!.EventKey);
        var rule = await db.NotificationDeliveryRules.SingleAsync(item => item.EventKey == "ManualCompanyNotice");
        Assert.True(rule.PortalEnabled);
        Assert.False(rule.MobileAppEnabled);
        Assert.False(rule.EmailEnabled);
        Assert.False(rule.WhatsAppEnabled);
        var log = await db.AuditLogEntries.SingleAsync();
        Assert.Equal("AdminNotificationTemplateCreated", log.Action);
        Assert.Equal("template-create", log.CorrelationId);
    }

    [Fact]
    public async Task CreateAsync_WhenTemplateAlreadyExists_ReturnsConflict()
    {
        await using var db = CreateDbContext();
        db.NotificationTemplates.Add(new NotificationTemplate(
            "MissionCancelled",
            NotificationTemplateChannel.MobilePush,
            "Annulation mission",
            "Customer",
            "Mission annulee",
            "Votre mission {NumeroMission} est annulee.",
            "{NumeroMission}"));
        await db.SaveChangesAsync();

        var result = await new AdminNotificationTemplateService(db).CreateAsync(
            new CreateNotificationTemplateRequest(
                "MissionCancelled",
                nameof(NotificationTemplateChannel.MobilePush),
                "Doublon",
                "Customer",
                "Sujet",
                "Message",
                null,
                true),
            CancellationToken.None);

        Assert.Equal(AdminNotificationTemplateStatus.Conflict, result.Status);
        Assert.Equal("Un modele existe deja pour cet evenement et ce canal.", result.Message);
    }

    [Fact]
    public async Task CreateAsync_WhenChannelIsInvalid_ReturnsValidationFailed()
    {
        await using var db = CreateDbContext();

        var result = await new AdminNotificationTemplateService(db).CreateAsync(
            new CreateNotificationTemplateRequest(
                "ManualCompanyNotice",
                "SmsLegacy",
                "Message portail entreprise",
                "Company",
                "Sujet",
                "Message",
                null,
                true),
            CancellationToken.None);

        Assert.Equal(AdminNotificationTemplateStatus.ValidationFailed, result.Status);
        Assert.Equal("Canal invalide. Utilisez Portal, MobilePush, Email ou WhatsApp.", result.Message);
    }

    [Fact]
    public async Task UpdateAsync_ShouldPersistTextActivationAndAudit()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var databaseRoot = new InMemoryDatabaseRoot();
        Guid templateId;

        await using (var db = CreateDbContext(databaseName, databaseRoot))
        {
            var template = new NotificationTemplate(
                "CompanyApplicationApproved",
                NotificationTemplateChannel.Email,
                "Ancien libelle",
                "Company",
                "Ancien sujet",
                "Ancien message",
                "{NomEntreprise}");
            db.NotificationTemplates.Add(template);
            await db.SaveChangesAsync();
            templateId = template.Id;

            var result = await new AdminNotificationTemplateService(db).UpdateAsync(
                templateId,
                new UpdateNotificationTemplateRequest(
                    "Dossier entreprise valide",
                    "Company",
                    "Votre dossier est valide",
                    "Bonjour {NomEntreprise}, votre portail est pret.",
                    "{NomEntreprise}",
                    false),
                AuditActor.Admin(),
                new AuditRequestContext("127.0.0.1", "tests", "template-update"),
                CancellationToken.None);

            Assert.Equal(AdminNotificationTemplateStatus.Ok, result.Status);
            Assert.False(result.Response!.IsActive);
        }

        await using (var db = CreateDbContext(databaseName, databaseRoot))
        {
            var template = await db.NotificationTemplates.SingleAsync(item => item.Id == templateId);
            Assert.Equal("Dossier entreprise valide", template.Label);
            Assert.Equal("Votre dossier est valide", template.SubjectTemplate);
            Assert.Equal("Bonjour {NomEntreprise}, votre portail est pret.", template.BodyTemplate);
            Assert.False(template.IsActive);
            var log = await db.AuditLogEntries.SingleAsync();
            Assert.Equal("AdminNotificationTemplateUpdated", log.Action);
            Assert.Equal(templateId, log.EntityId);
            Assert.Equal("template-update", log.CorrelationId);
            Assert.Contains("Ancien libelle", log.BeforeJson);
            Assert.Contains("Dossier entreprise valide", log.AfterJson);
        }
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
