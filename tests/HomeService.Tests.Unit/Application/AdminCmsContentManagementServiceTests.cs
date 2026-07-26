using HomeService.Application.Admin;
using HomeService.Application.Auditing;
using HomeService.Contracts.Cms;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class AdminCmsContentManagementServiceTests
{
    [Fact]
    public async Task UpdateContentValueAsync_WhenValueExists_UpdatesAndAudits()
    {
        await using var db = CreateDbContext();
        var value = new CmsContentValue(Guid.NewGuid(), "hero.title", CmsContentValueType.ShortText);
        value.SetText("Ancien titre");
        db.CmsContentValues.Add(value);
        await db.SaveChangesAsync();

        var result = await new AdminCmsContentManagementService(db).UpdateContentValueAsync(
            value.Id,
            new UpdateCmsContentValueRequest("Nouveau titre", null),
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "tests", "cms-update"),
            CancellationToken.None);

        Assert.Equal(AdminCmsContentManagementStatus.Ok, result.Status);
        Assert.Equal("Nouveau titre", result.Response!.TextValue);
        Assert.Contains(await db.AuditLogEntries.ToListAsync(), entry =>
            entry.Action == "AdminCmsContentValueUpdated"
            && entry.EntityId == value.Id
            && entry.CorrelationId == "cms-update");
    }

    [Fact]
    public async Task AttachMediaAsync_WhenValueExists_AttachesMediaAndAudits()
    {
        await using var db = CreateDbContext();
        var value = new CmsContentValue(Guid.NewGuid(), "hero.image", CmsContentValueType.Media);
        db.CmsContentValues.Add(value);
        await db.SaveChangesAsync();

        var media = new CmsMediaAsset("hero.webp", "cms/2026/07/hero.webp", "image/webp", 1024);
        media.MarkAvailable();
        var result = await new AdminCmsContentManagementService(db).AttachMediaAsync(
            value.Id,
            media,
            $"/api/cms/media/{media.Id}",
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "tests", "cms-media"),
            CancellationToken.None);

        Assert.Equal(AdminCmsContentManagementStatus.Ok, result.Status);
        Assert.Equal(media.Id, result.Response!.MediaAssetId);
        Assert.Equal($"/api/cms/media/{media.Id}", result.Response.Url);
        Assert.Equal(media.Id, await db.CmsMediaAssets.Select(item => item.Id).SingleAsync());

        var updatedValue = await db.CmsContentValues.SingleAsync(item => item.Id == value.Id);
        Assert.Equal(media.Id, updatedValue.MediaAssetId);
        Assert.Equal($"/api/cms/media/{media.Id}", updatedValue.TextValue);
        Assert.Contains(await db.AuditLogEntries.ToListAsync(), entry =>
            entry.Action == "AdminCmsMediaUploaded"
            && entry.EntityId == value.Id
            && entry.CorrelationId == "cms-media");
    }

    [Fact]
    public async Task UpdateContentValueAsync_WhenValueDoesNotExist_ReturnsNotFound()
    {
        await using var db = CreateDbContext();

        var result = await new AdminCmsContentManagementService(db).UpdateContentValueAsync(
            Guid.NewGuid(),
            new UpdateCmsContentValueRequest("Texte", null),
            AuditActor.Admin(),
            null,
            CancellationToken.None);

        Assert.Equal(AdminCmsContentManagementStatus.NotFound, result.Status);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }
}
