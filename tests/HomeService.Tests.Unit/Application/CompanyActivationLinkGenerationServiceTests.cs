using HomeService.Application.Auditing;
using HomeService.Application.Companies;
using HomeService.Application.Notifications;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HomeService.Tests.Unit.Application;

public sealed class CompanyActivationLinkGenerationServiceTests
{
    [Fact]
    public async Task GenerateAsync_WhenApplicationIsApproved_CreatesActivationLinkNotificationsAndAudit()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var databaseRoot = new InMemoryDatabaseRoot();
        Guid applicationId;
        await using (var setupDb = CreateDbContext(databaseName, databaseRoot))
        {
            var application = AddApprovedApplication(setupDb);
            applicationId = application.Id;
            await setupDb.SaveChangesAsync();
        }

        await using var db = CreateDbContext(databaseName, databaseRoot);
        var sut = CreateService(db);

        var result = await sut.GenerateAsync(
            applicationId,
            "https://company.wele.ci",
            tokenLifetimeHours: 6,
            changedBy: "admin@wele.ci",
            AuditActor.Admin("admin@wele.ci"),
            new AuditRequestContext("127.0.0.1", "unit-tests", "activation-link"),
            CancellationToken.None);

        Assert.True(result.Status == CompanyActivationLinkGenerationStatus.Ok, result.Message);
        Assert.NotNull(result.Response);
        Assert.Equal(CompanyApplicationStatus.Approved, result.PreviousStatus);
        Assert.Equal("ActivationSent", result.Response.Status);
        Assert.StartsWith("https://company.wele.ci", result.Response.ActivationLink);

        var savedApplication = await db.CompanyApplications.SingleAsync(item => item.Id == applicationId);
        Assert.Equal(CompanyApplicationStatus.ActivationSent, savedApplication.Status);
        Assert.NotNull(savedApplication.ActivationEmailSentAt);

        var token = await db.CompanyActivationTokens.SingleAsync(item => item.CompanyApplicationId == applicationId);
        Assert.Null(token.RevokedAt);
        Assert.Null(token.UsedAt);
        Assert.Contains(applicationId.ToString(), token.ActivationLink, StringComparison.OrdinalIgnoreCase);

        Assert.True(await db.CompanyApplicationStatusHistories.AnyAsync(history =>
            history.CompanyApplicationId == applicationId
            && history.PreviousStatus == CompanyApplicationStatus.Approved
            && history.NewStatus == CompanyApplicationStatus.ActivationSent));
        Assert.Equal(2, await db.NotificationOutboxMessages.CountAsync());
        Assert.Contains(await db.NotificationOutboxMessages.ToListAsync(), message =>
            message.Channel == NotificationChannel.Email
            && message.Recipient == "direction@wele.ci"
            && message.MetadataJson is not null
            && message.MetadataJson.Contains("activationLink", StringComparison.Ordinal));
        Assert.True(await db.AuditLogEntries.AnyAsync(log =>
            log.Action == "AdminCompanyActivationLinkGenerated"
            && log.EntityId == applicationId
            && log.CorrelationId == "activation-link"));
    }

    [Fact]
    public async Task GenerateAsync_WhenApplicationAlreadyHasActiveToken_RevokesPreviousTokenAndMarksReminder()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var databaseRoot = new InMemoryDatabaseRoot();
        Guid applicationId;
        await using (var setupDb = CreateDbContext(databaseName, databaseRoot))
        {
            var application = AddApprovedApplication(setupDb);
            applicationId = application.Id;
            await setupDb.SaveChangesAsync();
        }

        await using (var firstDb = CreateDbContext(databaseName, databaseRoot))
        {
            var firstResult = await CreateService(firstDb).GenerateAsync(
                applicationId,
                "https://company.wele.ci",
                tokenLifetimeHours: 6,
                changedBy: "admin@wele.ci",
                CancellationToken.None);
            Assert.True(firstResult.Status == CompanyActivationLinkGenerationStatus.Ok, firstResult.Message);
        }

        await using var db = CreateDbContext(databaseName, databaseRoot);
        var secondResult = await CreateService(db).GenerateAsync(
            applicationId,
            "https://company.wele.ci",
            tokenLifetimeHours: 6,
            changedBy: "admin@wele.ci",
            CancellationToken.None);

        Assert.True(secondResult.Status == CompanyActivationLinkGenerationStatus.Ok, secondResult.Message);
        Assert.Equal(CompanyApplicationStatus.ActivationSent, secondResult.PreviousStatus);

        var tokens = await db.CompanyActivationTokens
            .Where(token => token.CompanyApplicationId == applicationId)
            .OrderBy(token => token.CreatedAt)
            .ToListAsync();
        Assert.Equal(2, tokens.Count);
        Assert.NotNull(tokens[0].RevokedAt);
        Assert.Null(tokens[1].RevokedAt);

        var savedApplication = await db.CompanyApplications.SingleAsync(item => item.Id == applicationId);
        Assert.NotNull(savedApplication.LastReminderSentAt);
        Assert.Equal(4, await db.NotificationOutboxMessages.CountAsync());
    }

    private static CompanyApplication AddApprovedApplication(HomeServiceDbContext db)
    {
        var application = new CompanyApplication(
            "Wele Services",
            null,
            "Abidjan",
            "Cocody",
            "Awa Kone",
            "direction@wele.ci",
            "+2250700000000",
            "Menage",
            4);
        application.Approve("admin@wele.ci");
        db.CompanyApplications.Add(application);
        return application;
    }

    private static CompanyActivationLinkGenerationService CreateService(HomeServiceDbContext db)
        => new(db, new NotificationDeliveryPreferenceService(db));

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
